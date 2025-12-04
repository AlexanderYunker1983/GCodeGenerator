using System;
using MugenMvvmToolkit;

namespace GCodeGenerator
{
    public class GCodeGeneratorMvvmApp : MvvmApplication
    {
        public override Type GetStartViewModelType()
        {
            return typeof(ViewModels.MainViewModel);
        }
    }
}