﻿using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BfntConverterApp
{
   public abstract class BindableBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null!)
        {
            if (object.Equals(storage, value)) 
                return false;

            storage = value;
                OnPropertyChanged(propertyName);
            return true;
        }
    }
}
