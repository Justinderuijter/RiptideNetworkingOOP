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
    /// <summary>An instanced handler for incoming <see cref="Riptide.Server"/> <see cref="Message"/>s.</summary>
    public abstract class InstancedClientMessageHandler
    {
        /// <summary>
        /// Holds all currently registered types that extend <see cref="InstancedClientMessageHandler"/>.<br/>
        /// Types get unregistered when they are collected by the Garbage Collector
        /// </summary>
        private static readonly HashSet<Type> registered = new HashSet<Type>();
        /// <summary>Holds all message ids registered by this <see cref="InstancedClientMessageHandler"/>.</summary>
        private readonly HashSet<ushort> messageIds;
        /// <summary>The <see cref="Riptide.Client"/> that this <see cref="InstancedClientMessageHandler"/> belongs to.</summary>
        protected Client Client { get; private set; }

        /// <summary>Handles initial setup of this <see cref="InstancedClientMessageHandler"/>.</summary>
        /// <param name="client">The <see cref="Riptide.Client"/> that this <see cref="InstancedClientMessageHandler"/> instance's methods will be registered to.</param>
        /// <remarks>
        /// Be advised to not create new instances at will, this process may be slow. Instead prefer to instantiate your <see cref="InstancedClientMessageHandler"/> objects at the beginning of your program.<br/><br/>
        /// Throws <see cref="DuplicateHandlerException"/> if an instance of this type already exists.
        /// </remarks>
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

        /// <summary>Adds the message IDs and their corresponding message handler methods to the <see cref="Riptide.Client"/>'s <see cref="MessageHandler"/>'s.</summary>
        /// <param name="type">The type of the child class.</param>
        /// <param name="client">The <see cref="Riptide.Client"/> that this <see cref="InstancedClientMessageHandler"/> instance's methods will be registered to.</param>
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

        /// <summary>Unregisters all of this instance's handler methods from the <see cref="Riptide.Client"/>.</summary>
        private void Unregister()
        {
            foreach (ushort id in messageIds)
            {
                Client.messageHandlers.Remove(id);
            }
        }

        //Could make this class disposable instead, but does that encourage short lived instances?
        /// <summary>Unregisters all of this instance's handler methods from the <see cref="Riptide.Client"/>.</summary>
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
