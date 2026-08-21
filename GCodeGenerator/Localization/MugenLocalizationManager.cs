using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using MugenMvvmToolkit.Binding;
using MugenMvvmToolkit.Binding.Interfaces.Models;
using MugenMvvmToolkit.Binding.Models;
using MugenMvvmToolkit.Interfaces.Models;

namespace GCodeGenerator.Localization
{
    public class MugenLocalizationManager : LocalizationManager, IDynamicObject, INotifyPropertyChanged
    {
        private const string ResourceName = "i18n";
        static readonly string[] TimeKindKeys = { "SecondsShort", "MinutesShort", "HoursShort" };

        public MugenLocalizationManager()
        {
            //Register the current object as resource object with alias 'i18n' to use it in bindings '$i18n.MyResource'.
            BindingServiceProvider.ResourceResolver.AddObject(ResourceName, new BindingResourceObject(this, true));
            BindingServiceProvider.ResourceResolver.AddMethod("TimeToKindString",
                new BindingResourceMethod(TimeToKindString, typeof(string)));
        }

        private string TimeToKindString(IList<Type> arg1, object[] arg2, IDataContext arg3)
        {
            var allSecs = (uint)arg2[0];
            var sb = new StringBuilder();

            var currentIndex = 0;
            var lastRound = allSecs;
            while (true)
            {
                if (currentIndex == TimeKindKeys.Length || lastRound == 0) break;
                var round = lastRound / 60;

                if (round > 0)
                {
                    sb.Insert(0, $"{lastRound - round * 60} {GetString(TimeKindKeys[currentIndex])} ");
                }
                else
                {
                    sb.Insert(0, $"{lastRound} {GetString(TimeKindKeys[currentIndex])} ");
                    break;
                }
                currentIndex++;
                lastRound = round;
            }
            return sb.ToString();
        }


        public IDisposable TryObserve(string member, IEventListener listener)
        {
            return null;
        }

        public void SetIndex(IList<object> indexes, IDataContext context)
        {
            throw new NotSupportedException();
        }

        public object GetIndex(IList<object> indexes, IDataContext context)
        {
            throw new NotSupportedException();
        }

        public object InvokeMember(string member, IList<object> args, IList<Type> typeArgs, IDataContext context)
        {
            throw new NotSupportedException();
        }

        public void SetMember(string member, IList<object> args)
        {
            throw new NotSupportedException();
        }

        public object GetMember(string member, IList<object> args)
        {
            return GetString(member);
        }

        public override void ChangeCulture(CultureInfo cultureInfo)
        {
            base.ChangeCulture(cultureInfo);
            OnPropertyChanged(null);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
