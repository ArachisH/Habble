using System;
using System.Reflection;
using System.Collections.Generic;

namespace Habble
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PhysicalCommandAttribute : Attribute
    {
        public string Name { get; }
        public MethodInfo Method { get; set; }

        public PhysicalCommandAttribute(string name)
        {
            Name = name;
        }

        public object Invoke(object instance, Queue<string> arguments)
        {
            var parameters = new List<object>();
            foreach (ParameterInfo parameter in Method.GetParameters())
            {
                Type memberType = (Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType);
                if (arguments.Count == 0)
                {
                    if (parameter.IsOptional)
                    {
                        parameters.Add(Type.Missing);
                        continue;
                    }
                    else if (memberType.IsValueType)
                    {
                        parameters.Add(Activator.CreateInstance(memberType));
                    }
                    else parameters.Add(null);
                    continue;
                }

                switch (Type.GetTypeCode(memberType))
                {
                    case TypeCode.Int32:
                    {
                        if (int.TryParse(arguments.Dequeue(), out int iValue))
                        {
                            parameters.Add(iValue);
                        }
                        else parameters.Add(default(int));
                        break;
                    }
                    case TypeCode.String:
                    {
                        parameters.Add(arguments.Dequeue());
                        break;
                    }
                    case TypeCode.Boolean:
                    {
                        if (bool.TryParse(arguments.Dequeue(), out bool bValue))
                        {
                            parameters.Add(bValue);
                        }
                        else parameters.Add(default(bool));
                        break;
                    }
                }
            }
            return Method?.Invoke(instance, parameters.ToArray());
        }
    }
}