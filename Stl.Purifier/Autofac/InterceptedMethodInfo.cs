using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Castle.DynamicProxy;

namespace Stl.Purifier.Autofac
{
    public class InterceptedMethodInfo
    {
        public MethodInfo Method { get; private set; } = null!;
        public Type OutputType { get; private set; } = null!;
        public bool ReturnsValueTask { get; private set; }
        public bool ReturnsComputed { get; private set; }
        public int CancellationTokenArgumentIndex { get; private set; } = -1;
        public int UsedArgumentsBitmap { get; private set; } = int.MaxValue;

        private InterceptedMethodInfo() {}

        public static InterceptedMethodInfo? Create(MethodInfo method)
        {
            var attribute = method.GetCustomAttribute<ComputedAttribute>(true);
            var isEnabled = attribute?.IsEnabled ?? true;
            if (!isEnabled)
                return null;

            var returnType = method.ReturnType;
            if (!returnType.IsGenericType)
                return null;

            var returnTypeGtd = returnType.GetGenericTypeDefinition();
            var returnsTask = returnTypeGtd == typeof(Task<>);
            var returnsValueTask = returnTypeGtd == typeof(ValueTask<>);
            if (!(returnsTask || returnsValueTask))
                return null;

            var outputType = returnType.GetGenericArguments()[0];
            var returnsComputed = false;
            if (outputType.IsGenericType) {
                var returnTypeArgGtd = outputType.GetGenericTypeDefinition();
                if (returnTypeArgGtd == typeof(IComputed<>)) {
                    returnsComputed = true;
                    outputType = outputType.GetGenericArguments()[0];
                }
            }

            var r = new InterceptedMethodInfo {
                Method = method,
                OutputType = outputType,
                ReturnsValueTask = returnsValueTask,
                ReturnsComputed = returnsComputed,
            };
            var index = 0;
            foreach (var p in method.GetParameters()) {
                if (typeof(CancellationToken).IsAssignableFrom(p.ParameterType))
                    r.CancellationTokenArgumentIndex = index;
                index++;
            }
            if (r.CancellationTokenArgumentIndex >= 0)
                r.UsedArgumentsBitmap ^= 1 << r.CancellationTokenArgumentIndex;
            return r;
        }
    }
}
