using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;

namespace FileClient
{
    /// <summary>
    /// Implements the basic functionality common to all view models.
    /// </summary>
    /// <typeparam name="T">Type of view model class.</typeparam>
    public abstract class ViewModelBase<T> : INotifyPropertyChanged
    {
        private readonly IDictionary<object, string> _expressionDictionary =
            new Dictionary<object, string>();

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Called when property changed.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// Sets the property value.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <param name="expression">The expression.</param>
        protected void SetProperty<TProperty>(ref TProperty field, TProperty value, Expression<Func<T, TProperty>> expression)
        {
            field = value;

            OnPropertyChanged(expression);
        }

        /// <summary>
        /// Called when property changed.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="expression">The expression.</param>
        private void OnPropertyChanged<TProperty>(Expression<Func<T, TProperty>> expression)
        {
            if (!_expressionDictionary.ContainsKey(expression))
            {
                var propertyName =
                    TypeExtensions<T>
                        .GetProperty(expression);

                _expressionDictionary.Add(expression, propertyName);
            }

            OnPropertyChanged(_expressionDictionary[expression]);
        }
    } 

}
