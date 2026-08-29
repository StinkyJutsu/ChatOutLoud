using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ChatOutLoud.TtsProviders;

internal sealed class BulkObservableCollection<T>
    : ObservableCollection<T>
{
    private bool _suppressNotifications;

    public void AddRange(
        IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _suppressNotifications =
            true;

        try
        {
            foreach (T item in items)
            {
                Items.Add(
                    item);
            }
        }
        finally
        {
            _suppressNotifications =
                false;
        }

        OnPropertyChanged(
            new PropertyChangedEventArgs(
                nameof(Count)));

        OnPropertyChanged(
            new PropertyChangedEventArgs(
                "Item[]"));

        OnCollectionChanged(
            new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
    }

    public int RemoveAll(
        Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        int removedCount =
            0;

        _suppressNotifications =
            true;

        try
        {
            for (int index =
                     Items.Count - 1;
                 index >= 0;
                 index--)
            {
                if (!match(
                        Items[index]))
                {
                    continue;
                }

                Items.RemoveAt(
                    index);

                removedCount++;
            }
        }
        finally
        {
            _suppressNotifications =
                false;
        }

        if (removedCount > 0)
        {
            OnPropertyChanged(
                new PropertyChangedEventArgs(
                    nameof(Count)));

            OnPropertyChanged(
                new PropertyChangedEventArgs(
                    "Item[]"));

            OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Reset));
        }

        return removedCount;
    }

    protected override void OnCollectionChanged(
        NotifyCollectionChangedEventArgs e)
    {
        if (_suppressNotifications)
        {
            return;
        }

        base.OnCollectionChanged(
            e);
    }

    protected override void OnPropertyChanged(
        PropertyChangedEventArgs e)
    {
        if (_suppressNotifications)
        {
            return;
        }

        base.OnPropertyChanged(
            e);
    }
}