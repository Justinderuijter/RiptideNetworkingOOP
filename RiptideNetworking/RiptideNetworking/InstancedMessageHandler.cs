// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using static Riptide.Server;

namespace Riptide
{
    public abstract class InstancedMessageHandler
    {
        private static readonly Dictionary<Type, byte> registered = new Dictionary<Type, byte>();
        protected Server Server { get; private set; }
        public InstancedMessageHandler(Server server)
        {
            Server = server;
            Type type = GetType();
            if (registered.ContainsKey(type))
            {
                registered[type]++;
            }
            else
            {
                RegisterMethods(type, server);
                registered.Add(type, 1);
            }
        }

        private void RegisterMethods(Type type, Server server)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.GetCustomAttributes(typeof(MessageHandlerAttribute), false).Length > 0)
                {
                    MessageHandlerAttribute attribute = method.GetCustomAttribute<MessageHandlerAttribute>();
                    Delegate serverMessageHandler = Delegate.CreateDelegate(typeof(MessageHandler), this, method.Name, false);
                    if (serverMessageHandler != null)
                    {
                        // It's a message handler for Server instances
                        if (server.messageHandlers.ContainsKey(attribute.MessageId))
                        {
                            MethodInfo otherMethodWithId = server.messageHandlers[attribute.MessageId].GetMethodInfo();
                            throw new DuplicateHandlerException(attribute.MessageId, method, otherMethodWithId);
                        }
                        else
                            server.messageHandlers.Add(attribute.MessageId, (MessageHandler)serverMessageHandler);
                    }
                    else
                    {
                        // It's not a message handler for Server instances, but it might be one for Client instances
                        Delegate clientMessageHandler = Delegate.CreateDelegate(typeof(Client.MessageHandler), method, false);
                        if (clientMessageHandler == null)
                            throw new InvalidHandlerSignatureException(method.DeclaringType, method.Name);
                    }
                }
            }
        }

        private void Unregister()
        {
        
        }

        ~InstancedMessageHandler()
        {
            Type type = GetType();

            if (--registered[type] == 0)
            {
                registered.Remove(type);
                //unregister
            }
        }


    }
}
