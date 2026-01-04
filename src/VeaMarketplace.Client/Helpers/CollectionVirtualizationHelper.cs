using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Virtualized observable collection for handling large lists with 1000+ items efficiently.
/// Only loads and renders visible items to reduce memory and CPU usage.
/// </summary>
public class VirtualizedObservableCollection<T> : IList<T>, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly List<T> _allItems = new();
    private readonly ObservableCollection<T> _visibleItems = new();
    private int _pageSize = 50;
    private int _currentPage = 0;
    private readonly object _lock = new();

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (_pageSize != value)
            {
                _pageSize = value;
                RefreshVisibleItems();
                OnPropertyChanged(nameof(PageSize));
            }
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (_currentPage != value)
            {
                _currentPage = Math.Max(0, Math.Min(value, TotalPages - 1));
                RefreshVisibleItems();
                OnPropertyChanged(nameof(CurrentPage));
            }
        }
    }

    public int TotalPages => (int)Math.Ceiling((double)Count / PageSize);
    public int TotalItems => _allItems.Count;

    /// <summary>
    /// Get the visible items (current page)
    /// </summary>
    public ObservableCollection<T> VisibleItems => _visibleItems;

    public VirtualizedObservableCollection()
    {
        _visibleItems.CollectionChanged += (s, e) => CollectionChanged?.Invoke(this, e);
    }

    public VirtualizedObservableCollection(IEnumerable<T> items) : this()
    {
        AddRange(items);
    }

    public void AddRange(IEnumerable<T> items)
    {
        lock (_lock)
        {
            _allItems.AddRange(items);
            RefreshVisibleItems();
        }
    }

    public void NextPage()
    {
        if (CurrentPage < TotalPages - 1)
        {
            CurrentPage++;
        }
    }

    public void PreviousPage()
    {
        if (CurrentPage > 0)
        {
            CurrentPage--;
        }
    }

    public void GoToFirstPage()
    {
        CurrentPage = 0;
    }

    public void GoToLastPage()
    {
        CurrentPage = TotalPages - 1;
    }

    private void RefreshVisibleItems()
    {
        lock (_lock)
        {
            _visibleItems.Clear();

            var startIndex = CurrentPage * PageSize;
            var endIndex = Math.Min(startIndex + PageSize, _allItems.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                _visibleItems.Add(_allItems[i]);
            }

            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(TotalItems));
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // IList<T> implementation
    public int Count => _allItems.Count;
    public bool IsReadOnly => false;

    public T this[int index]
    {
        get => _allItems[index];
        set
        {
            _allItems[index] = value;
            RefreshVisibleItems();
        }
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            _allItems.Add(item);
            RefreshVisibleItems();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _allItems.Clear();
            _visibleItems.Clear();
            _currentPage = 0;
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(TotalPages));
        }
    }

    public bool Contains(T item) => _allItems.Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => _allItems.CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => _allItems.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int IndexOf(T item) => _allItems.IndexOf(item);

    public void Insert(int index, T item)
    {
        lock (_lock)
        {
            _allItems.Insert(index, item);
            RefreshVisibleItems();
        }
    }

    public bool Remove(T item)
    {
        lock (_lock)
        {
            var result = _allItems.Remove(item);
            if (result)
            {
                RefreshVisibleItems();
            }
            return result;
        }
    }

    public void RemoveAt(int index)
    {
        lock (_lock)
        {
            _allItems.RemoveAt(index);
            RefreshVisibleItems();
        }
    }
}

/// <summary>
/// Lazy-loading collection that loads items on demand for optimal memory usage with 1000+ items
/// </summary>
public class LazyLoadingCollection<T> : ObservableCollection<T>
{
    private readonly Func<int, int, Task<List<T>>> _loadItemsFunc;
    private readonly int _pageSize;
    private int _currentPage = 0;
    private bool _isLoading = false;
    private bool _hasMoreItems = true;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsLoading)));
            }
        }
    }

    public bool HasMoreItems
    {
        get => _hasMoreItems;
        private set
        {
            if (_hasMoreItems != value)
            {
                _hasMoreItems = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasMoreItems)));
            }
        }
    }

    public LazyLoadingCollection(Func<int, int, Task<List<T>>> loadItemsFunc, int pageSize = 50)
    {
        _loadItemsFunc = loadItemsFunc;
        _pageSize = pageSize;
    }

    public async Task LoadMoreAsync()
    {
        if (IsLoading || !HasMoreItems)
            return;

        IsLoading = true;

        try
        {
            var items = await _loadItemsFunc(_currentPage, _pageSize);

            if (items.Count == 0)
            {
                HasMoreItems = false;
            }
            else
            {
                foreach (var item in items)
                {
                    Add(item);
                }

                if (items.Count < _pageSize)
                {
                    HasMoreItems = false;
                }
                else
                {
                    _currentPage++;
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Reset()
    {
        Clear();
        _currentPage = 0;
        HasMoreItems = true;
    }
}

/// <summary>
/// Efficient filtered collection for real-time filtering without creating new collections
/// </summary>
public class FilteredObservableCollection<T> : ObservableCollection<T>
{
    private readonly ObservableCollection<T> _sourceCollection;
    private Predicate<T>? _filter;

    public FilteredObservableCollection(ObservableCollection<T> sourceCollection)
    {
        _sourceCollection = sourceCollection;
        _sourceCollection.CollectionChanged += SourceCollection_CollectionChanged;

        // Initial population
        RefreshFilter();
    }

    public Predicate<T>? Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            RefreshFilter();
        }
    }

    private void RefreshFilter()
    {
        Clear();

        foreach (var item in _sourceCollection)
        {
            if (_filter == null || _filter(item))
            {
                Add(item);
            }
        }
    }

    private void SourceCollection_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    foreach (T item in e.NewItems)
                    {
                        if (_filter == null || _filter(item))
                        {
                            Add(item);
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    foreach (T item in e.OldItems)
                    {
                        Remove(item);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                RefreshFilter();
                break;

            case NotifyCollectionChangedAction.Replace:
                RefreshFilter();
                break;
        }
    }
}
