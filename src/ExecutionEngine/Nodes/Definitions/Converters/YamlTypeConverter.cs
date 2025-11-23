// -----------------------------------------------------------------------
// <copyright file="YamlTypeConverter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions.Converters
{
    using YamlDotNet.Core;
    using YamlDotNet.Serialization;

    /// <summary>
    /// Generic base class for strongly-typed YAML converters
    /// </summary>
    public abstract class YamlTypeConverter<T> : IYamlTypeConverter where T : class, new()
    {
        public bool Accepts(Type type)
        {
            return type == typeof(T);
        }

        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            return this.Read(parser, type, rootDeserializer);
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        {
            this.Write(emitter, (T?)value, type, serializer);
        }

        // Strongly-typed abstract methods for derived classes
        public abstract T? Read(IParser parser, Type type, ObjectDeserializer rootDeserializer);

        public abstract void Write(IEmitter emitter, T? value, Type type, ObjectSerializer serializer);
    }
}