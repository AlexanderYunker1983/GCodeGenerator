using System;

namespace GCodeGenerator.Core.Attributes;

/// <summary>
/// Атрибут для пометки свойств, которые должны обновляться при смене локали
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class LocalizedAttribute : Attribute
{
}

