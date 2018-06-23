using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TcpSharpr.Network;

namespace TcpSharpr.MethodInteraction {
    public class CommandManager {
        protected List<Command> _registeredCommands;

        public CommandManager() {
            _registeredCommands = new List<Command>();
        }

        public CommandManager RegisterAsyncCommand(string commandName, Delegate targetAsyncMethod) {
            lock (_registeredCommands) {
                _registeredCommands.Add(new Command(true, commandName, targetAsyncMethod));
            }

            return this;
        }

        public CommandManager RegisterCommand(string commandName, Delegate targetMethod) {
            lock (_registeredCommands) {
                _registeredCommands.Add(new Command(false, commandName, targetMethod));
            }

            return this;
        }

        public async Task<object> InvokeCommand(string commandName, object[] arguments) {
            Command targetCommand = null;

            lock (_registeredCommands) {
                foreach (var command in _registeredCommands) {
                    if (command.Name.ToLower().Equals(commandName.ToLower())) {
                        targetCommand = command;
                        break;
                    }
                }
            }

            if (targetCommand == null) {
                throw new KeyNotFoundException("<commandName> is not a registered command.");
            }

            if (targetCommand.IsAsync) {
                Task executionTask = targetCommand.Delegate.DynamicInvoke(arguments) as Task;

                // Await task
                await executionTask;

                // Return result. If the async method has void as the return type, null will be returned instead.
                return executionTask.GetType().GetProperty("Result").GetValue(executionTask);
            } else {
                object resultValue = targetCommand.Delegate.DynamicInvoke(arguments);
                return resultValue;
            }
        }

        public class Command {
            public bool IsAsync { get; private set; }
            public string Name { get; private set; }
            public Delegate Delegate { get; private set; }

            public Command(bool isAsync, string name, Delegate @delegate) {
                IsAsync = isAsync;
                Name = name;
                Delegate = @delegate;
            }
        }
    }
}
