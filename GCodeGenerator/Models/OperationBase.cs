using System;

namespace GCodeGenerator.Models
{
    public abstract class OperationBase
    {
        protected OperationBase(OperationType type, string name)
        {
            Type = type;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public OperationType Type { get; }

        /// <summary>
        /// User-friendly name of operation, shown in UI.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Short human readable description for list in UI.
        /// </summary>
        public abstract string GetDescription();
    }
}


