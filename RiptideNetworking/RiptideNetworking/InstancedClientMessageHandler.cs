// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Reflection;
using static Riptide.Client;

namespace Riptide
{
    public abstract class InstancedClientMessageHandler
    {
        private static readonly HashSet<Type> registered = new HashSet<Type>();
        private readonly HashSet<ushort> messageIds;
        protected Client Client { get; private set; }
        
        public InstancedClientMessageHandler(Client client)
        {
            Client = client;
            messageIds = new HashSet<ushort>();

            Type type = GetType();
            if (!registered.Contains(type))
            {
                RegisterMethods(type, client);
                registered.Add(type);
            }
            else
            {
                throw new DuplicateHandlerException("An " + nameof(InstancedClientMessageHandler) + " of type " + type.Name + "is already registered.");
            }
        }

        private void RegisterMethods(Type type, Client client)
        {
            if (client.messageHandlers == null)
            {
                client.messageHandlers = new Dictionary<ushort, MessageHandler>();
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.GetCustomAttributes(typeof(MessageHandlerAttribute), false).Length > 0)
                {
                    MessageHandlerAttribute attribute = method.GetCustomAttribute<MessageHandlerAttribute>();
                    Delegate clientMessageHandler = Delegate.CreateDelegate(typeof(MessageHandler), this, method.Name, false);
                    if (clientMessageHandler != null)
                    {
                        // It's a message handler for Client instances
                        if (client.messageHandlers.ContainsKey(attribute.MessageId))
                        {
                            MethodInfo otherMethodWithId = client.messageHandlers[attribute.MessageId].GetMethodInfo();
                            throw new DuplicateHandlerException(attribute.MessageId, method, otherMethodWithId);
                        }
                        else
                        {
                            client.messageHandlers.Add(attribute.MessageId, (MessageHandler)clientMessageHandler);
                            messageIds.Add(attribute.MessageId);
                        }
                              
                    }
                    else
                    {
                        // It's not a message handler for Client instances, but it might be one for Server instances
                        Delegate serverMessageHandler = Delegate.CreateDelegate(typeof(Server.MessageHandler), method, false);
                        if (serverMessageHandler == null)
                            throw new InvalidHandlerSignatureException(method.DeclaringType, method.Name);
                    }
                }
            }
        }

        private void Unregister()
        {
            foreach (ushort id in messageIds)
            {
                Client.messageHandlers.Remove(id);
            }
        }

        //Could make this class disposable instead, but does that encourage short lived instances?
        ~InstancedClientMessageHandler()
        {
            Type type = GetType();

            if (registered.Contains(type))
            {
                Unregister();
                registered.Remove(type);
            }
        }
    }
}
