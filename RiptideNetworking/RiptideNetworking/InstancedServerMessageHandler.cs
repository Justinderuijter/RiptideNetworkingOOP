// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Reflection;
using static Riptide.Server;

namespace Riptide
{
    /// <summary>An instanced handler for incoming <see cref="Client"/> <see cref="Message"/>s.</summary>
    public abstract class InstancedServerMessageHandler: IDisposable
    {
        /// <summary>
        /// Holds all currently registered types that extend <see cref="InstancedServerMessageHandler"/>.<br/>
        /// Types get unregistered when they are collected by the Garbage Collector
        /// </summary>
        private static readonly HashSet<Type> registered = new HashSet<Type>();
        /// <summary>Holds all message ids registered by this <see cref="InstancedServerMessageHandler"/>.</summary>
        private readonly HashSet<ushort> messageIds;
        /// <summary>The <see cref="Riptide.Server"/> that this <see cref="InstancedServerMessageHandler"/> belongs to.</summary>
        protected Server Server { get; private set; }

        /// <summary>Handles initial setup of this <see cref="InstancedServerMessageHandler"/>.</summary>
        /// <param name="server">The <see cref="Riptide.Server"/> that this <see cref="InstancedServerMessageHandler"/> instance's methods will be registered to.</param>
        /// <remarks>
        /// Be advised to not create new instances at will, this process may be slow. Instead prefer to instantiate your <see cref="InstancedServerMessageHandler"/> objects at the beginning of your program.<br/><br/>
        /// Throws <see cref="DuplicateHandlerException"/> if an instance of this type already exists.
        /// </remarks>
        public InstancedServerMessageHandler(Server server)
        {
            Server = server;
            messageIds = new HashSet<ushort>();

            Type type = GetType();
            if (!registered.Contains(type))
            {
                RegisterMethods(type, server);
                registered.Add(type);
            }
            else
            {
                throw new DuplicateHandlerException("An " + nameof(InstancedClientMessageHandler) + " of type " + type.Name + "is already registered.");
            }
        }

        /// <summary>Adds the message IDs and their corresponding message handler methods to the <see cref="Riptide.Server"/>'s <see cref="MessageHandler"/>'s.</summary>
        /// <param name="type">The type of the child class.</param>
        /// <param name="server">The <see cref="Riptide.Server"/> that this <see cref="InstancedServerMessageHandler"/> instance's methods will be registered to.</param>
        private void RegisterMethods(Type type, Server server)
        {
            if (server.messageHandlers == null)
            {
                server.messageHandlers = new Dictionary<ushort, MessageHandler>();
            }

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
                        {
                            server.messageHandlers.Add(attribute.MessageId, (MessageHandler)serverMessageHandler);
                            messageIds.Add(attribute.MessageId);
                        }

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

        /// <summary>Unregisters all of this instance's handler methods from the <see cref="Riptide.Server"/>.</summary>
        private void UnregisterHandlers()
        {
            foreach (ushort id in messageIds)
            {
                Server.messageHandlers.Remove(id);
            }
        }

        private void Unregister()
        {
            Type type = GetType();

            if (registered.Contains(type))
            {
                UnregisterHandlers();
                registered.Remove(type);
            }
        }

        /// <summary>Unregisters all of this instance's handler methods from the <see cref="Riptide.Server"/>.</summary>
        public void Dispose()
        {
            Unregister();
            GC.SuppressFinalize(this);
        }

        /// <summary>Unregisters all of this instance's handler methods from the <see cref="Riptide.Server"/>.</summary>
        /// <remarks>This finalizer only runs when the object is garbage collected before <see cref="Dispose"/> is ran.</remarks>
        ~InstancedServerMessageHandler()
        {
            Unregister();
        }
    }
}
