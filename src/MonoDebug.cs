﻿/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace VSCodeDebug
{
	internal class Program
	{
		const int DEFAULT_PORT = 4711;

		private static bool trace_dap;

		private static void Main(string[] argv)
		{
			int port = -1;

			// parse command line arguments
			foreach (var a in argv) {
				switch (a) {
				case "--trace=dap":
					trace_dap = true;
					break;
				case "--server":
					port = DEFAULT_PORT;
					break;
				default:
					if (a.StartsWith("--server=", StringComparison.Ordinal)) {
						if (!int.TryParse(a.Substring("--server=".Length), out port)) {
							port = DEFAULT_PORT;
						}
					}
					break;
				}
			}

			if (port > 0) {
				// TCP/IP server
				Console.Error.WriteLine("waiting for debug protocol on port " + port);
				RunServer(port);
			} else {
				// stdin/stdout
				Console.Error.WriteLine("waiting for debug protocol on stdin/stdout");
				var debugSession = new MonoDebugSession(Console.OpenStandardInput(), Console.OpenStandardOutput());
				debugSession.Protocol.Run();
			}
		}

		private static void RunServer(int port)
		{
			TcpListener serverSocket = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
			serverSocket.Start();

			new System.Threading.Thread(() => {
				while (true) {
					var clientSocket = serverSocket.AcceptSocket();
					if (clientSocket != null) {
						Console.Error.WriteLine(">> accepted connection from client");

						new System.Threading.Thread(() => {
							using (var networkStream = new NetworkStream(clientSocket)) {
								try {
									var adapter = new MonoDebugSession(networkStream, networkStream);
									if (trace_dap) {
										adapter.Protocol.TraceCallback = (s) => Console.WriteLine(s);
									}
									adapter.Protocol.DispatcherError += (sender, e) => {
										Console.Error.WriteLine(e.Exception.Message);
									};
									adapter.Protocol.Run();
									adapter.Protocol.WaitForReader();
								}
								catch (Exception e) {
									Console.Error.WriteLine("Exception: " + e);
								}
							}
							clientSocket.Close();
							Console.Error.WriteLine(">> client connection closed");
						}).Start();
					}
				}
			}).Start();
		}
	}
}
