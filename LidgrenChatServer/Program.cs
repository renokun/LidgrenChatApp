using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Lidgren.Network;

namespace LidgrenChatServer
{
    class Program
    {
        private static SortedList<Int64, String> uidNameMap = new SortedList<long, string>();
        private static NetServer server;
        private static INetEncryption algo = new NetXtea("SecretKey0101");
        private static string adminCode = "admincode";
        private static Random rnd = new Random();
        private static string welcomeStr = "Welcome! Please use /nick to change your nickname. " + 
            "To see a list of online users, type /list.";

        static void Main(string[] args)
        {
            // create a configuration
            NetPeerConfiguration config = new NetPeerConfiguration("LidgrenChat"); // needs to be same on client and server!
            config.MaximumConnections = 64;
            config.Port = 7777;

            // Server configs
            if (File.Exists("serverconfig.txt"))
            {
                using (StreamReader sr = new StreamReader("serverconfig.txt"))
                {
                    config.Port = Convert.ToInt32(sr.ReadLine().Split('=')[1]);
                    algo = new NetXtea(sr.ReadLine().Split('=')[1]);
                    adminCode = sr.ReadLine().Split('=')[1];
                    welcomeStr = sr.ReadLine().Split('=')[1];
                }
            }

            server = new NetServer(config);
            server.Start();

            Console.WriteLine("Server has started on port " + server.Port);

            while (true)
            {
                RecieveAndSend();
            }
        }

        static void UpdateUIDMap(long uid, string nickname)
        {
            if (uidNameMap.ContainsKey(uid))
            {
                uidNameMap[uid] = nickname;
            }
            else
            {
                uidNameMap.Add(uid, nickname);
            }
        }

        static string ProcessCommands(string[] commands, NetConnection sender)
        {
            try
            {
                string stringBuffer = String.Empty;
                string messageToSender = String.Empty;
                NetOutgoingMessage outMsg = server.CreateMessage();

                if (commands.Length > 1 && commands[0] == "nick")
                {
                    string nickName = commands[1];

                    if (!String.IsNullOrWhiteSpace(nickName))
                    {
                        UpdateUIDMap(sender.RemoteUniqueIdentifier, nickName);
                        stringBuffer += sender.RemoteUniqueIdentifier +
                            " (" + sender.RemoteEndpoint.Address + ")" +
                            " has changed name to " + uidNameMap[sender.RemoteUniqueIdentifier] + Environment.NewLine;
                    }
                }
                else if (commands.Length > 2 && commands[0] == "random")
                {
                    int min, max;
                    if (Int32.TryParse(commands[1], out min) && Int32.TryParse(commands[2], out max))
                    {
                        stringBuffer += "Random number generated between " + min + " and " + max + ": " +
                            rnd.Next(min, max) + Environment.NewLine;
                    }
                }
                else if (commands.Length > 0 && commands[0] == "random")
                {
                    stringBuffer += "Random number generated: " + rnd.Next() + Environment.NewLine;
                }
                else if (commands.Length > 2 && commands[0] == "kick")
                {
                    // kicks a user with UID
                    if (commands[2] == adminCode)
                    {
                        foreach (NetConnection connection in server.Connections)
                        {
                            if (connection.RemoteUniqueIdentifier.ToString() == commands[1])
                            {
                                //connection.Deny("kicked");
                                connection.Disconnect("kicked");
                                stringBuffer += uidNameMap[connection.RemoteUniqueIdentifier] +
                                    " has been kicked by " + uidNameMap[sender.RemoteUniqueIdentifier] +
                                    Environment.NewLine;
                                break;
                            }
                        }
                    }
                }
                else if (commands.Length > 2 && commands[0] == "real")
                {
                    // print out all the info for this user
                    if (commands[2] == adminCode)
                    {
                        foreach (NetConnection connection in server.Connections)
                        {
                            string nick = uidNameMap[connection.RemoteUniqueIdentifier];
                            if (nick == commands[1])
                            {
                                messageToSender += "===== QUERY RESULT =====" + Environment.NewLine +
                                    "UID: " + connection.RemoteUniqueIdentifier + Environment.NewLine +
                                    "Nick: " + nick + Environment.NewLine +
                                    "IP: " + connection.RemoteEndpoint.Address + Environment.NewLine +
                                    "Port: " + connection.RemoteEndpoint.Port + Environment.NewLine +
                                    "===== END OF RESULT =====" + Environment.NewLine;
                            }
                        }
                    }
                }
                else if (commands.Length > 0 && commands[0] == "list")
                {
                    messageToSender += "===== ONLINE USERS =====" + Environment.NewLine;
                    foreach (NetConnection connection in server.Connections)
                    {
                        long uid = connection.RemoteUniqueIdentifier;
                        messageToSender += uid + ": " + uidNameMap[uid] + Environment.NewLine;
                    }
                    messageToSender += "TOTAL: " + server.ConnectionsCount + Environment.NewLine +
                        "===== END OF RESULT =====" + Environment.NewLine;
                }

                // send a message to sender, if messageToSender is not empty
                if (!String.IsNullOrEmpty(messageToSender))
                {
                    outMsg.Write(messageToSender);
                    outMsg.Encrypt(algo);
                    server.SendMessage(outMsg, sender, NetDeliveryMethod.ReliableOrdered);
                }

                return stringBuffer;
            }
            catch (Exception exc)
            {
                Console.WriteLine("Error on command " + commands + " sent by " + 
                    sender.RemoteUniqueIdentifier + Environment.NewLine + exc.ToString());
                return String.Empty;
            }
        }

        static void RecieveAndSend()
        {
            string serverStringBuffer = String.Empty;
            NetIncomingMessage msg;
            while ((msg = server.ReadMessage()) != null)
            {
                long senderUid = msg.SenderConnection == null ? 0 : msg.SenderConnection.RemoteUniqueIdentifier;
                string senderIpAddress = msg.SenderEndpoint.Address.ToString();
                msg.Decrypt(algo);
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.WarningMessage:
                    case NetIncomingMessageType.ErrorMessage:
                        Console.WriteLine(msg.ReadString());
                        break;

                    case NetIncomingMessageType.Data:
                        string dataString = msg.ReadString();
                        //if (!uidNameMap.ContainsKey(senderUid))
                        //{
                        //    UpdateUIDMap(senderUid, msg.ReadString());
                        //    serverStringBuffer += senderUid + 
                        //        " (" + senderIpAddress + ")"
                        //        + " has changed name to " + uidNameMap[senderUid] + Environment.NewLine;
                        //}
                        //else
                        //{
                        
                        //}
                        if (dataString.StartsWith("/"))
                        {
                            // Don't show this message to client, but still want it in console
                            Console.WriteLine(uidNameMap[senderUid] + ": " + dataString);
                            string[] commands = dataString.Remove(0, 1).Split(' ');
                            serverStringBuffer += ProcessCommands(commands, msg.SenderConnection);
                        }
                        else
                        {
                            serverStringBuffer += uidNameMap[senderUid] + ": " + dataString + Environment.NewLine;
                        }
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        serverStringBuffer += "User status changed: " + 
                            senderIpAddress + " " +
                            (uidNameMap.ContainsKey(senderUid) ? uidNameMap[senderUid] + " " : "") +
                            msg.SenderConnection.Status + Environment.NewLine;
                        // If the status is connected, check the UID map
                        if (msg.SenderConnection.Status == NetConnectionStatus.Connected &&
                            !uidNameMap.ContainsKey(senderUid))
                        {
                            // add user uid to uidmap
                            UpdateUIDMap(senderUid, senderUid.ToString());
                            NetOutgoingMessage nameSetMsg = server.CreateMessage();
                            nameSetMsg.Write(welcomeStr + Environment.NewLine);
                            nameSetMsg.Encrypt(algo);
                            server.SendMessage(nameSetMsg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                        }

                        // If user is disconnecting, remove his uid in map
                        if (msg.SenderConnection.Status == NetConnectionStatus.Disconnected)
                        {
                            uidNameMap.Remove(msg.SenderConnection.RemoteUniqueIdentifier);
                        }
                        break;

                    default:
                        Console.WriteLine("Unhandled type: " + msg.MessageType +
                            "; Read string: " + msg.ReadString());
                        break;
                }
                server.Recycle(msg);

                // Display content of server string buffer on screen
                Console.Write(serverStringBuffer);
                // Send to all clients
                NetOutgoingMessage outMsg = server.CreateMessage();
                outMsg.Write(serverStringBuffer);
                outMsg.Encrypt(algo);
                server.SendToAll(outMsg, NetDeliveryMethod.ReliableOrdered);
            }
        }
    }
}
