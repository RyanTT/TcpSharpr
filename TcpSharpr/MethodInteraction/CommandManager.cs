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

        public async Task<object> InvokeCommand(NetworkClient context, string commandName, object[] arguments, bool waitForReturn) {
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

            // Build parameter list
            var parameters = new List<object>();
            var givenParameters = new Queue<object>(arguments);
            var parameterInfos = targetCommand.Delegate.Method.GetParameters();

            foreach (var expectedParameter in parameterInfos) {
                if (expectedParameter.ParameterType == typeof(NetworkClient)) {
                    parameters.Add(context);
                } else {
                    if (givenParameters.Count == 0) {
                        throw new ArgumentException("The specificed command has a different signature than the given arguments suggest.");
                    }

                    parameters.Add(givenParameters.Dequeue());
                }
            }

            if (targetCommand.IsAsync) {
                Task executionTask = targetCommand.Delegate.DynamicInvoke(parameters.ToArray()) as Task;

                if (waitForReturn) {
                    if (executionTask.IsFaulted) {
                        throw new Exception("The invoked async delegate produced a faulted task.");
                    }

                    // Await task
                    await executionTask;

                    // Return result. If the async method has void as the return type, null will be returned instead.
                    var returnValue = executionTask.GetType().GetProperty("Result").GetValue(executionTask);

                    if (targetCommand.Delegate.Method.ReturnType != typeof(Task)) {
                        return returnValue;
                    }
                }

                return null;
            } else {
                object resultValue = targetCommand.Delegate.DynamicInvoke(parameters.ToArray());
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
