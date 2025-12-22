using System;

namespace GCodeGenerator.Core.Attributes;

/// <summary>
/// Атрибут для пометки свойств, которые должны отображаться и редактироваться в панели свойств.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PropertyEditorAttribute : Attribute
{
    /// <summary>
    /// Ключ строкового ресурса для отображаемого имени свойства.
    /// </summary>
    public string ResourceKey { get; }

    /// <summary>
    /// Порядок отображения свойства в панели.
    /// </summary>
    public int Order { get; set; }

    public PropertyEditorAttribute(string resourceKey)
    {
        ResourceKey = resourceKey;
    }
}


