// Lab 2: the shared stock, order queue, workers and alerts.
using System.Collections.Concurrent;
using System.Globalization;

namespace Lab2;

public sealed class Shop
{
    public const int MaxStock = 10_000;
    private const int LowStock = 5;

    // Guards stock, active orders, provider and numbering.
    private readonly object _lock = new();

    private readonly Product[] _products =
    {
        new Product("coffee", 4),
        new Product("tea", 3),
        new Product("croissant", 2),
    };

    private readonly BlockingCollection<Order> _queue = new();
    private readonly List<Order> _activeOrders = new();
    private readonly AutoResetEvent _stockChanged = new(false);
    private readonly string _stockFile = Path.Combine(AppContext.BaseDirectory, "stock.txt");
    private Connection? _provider;
    private int _nextNumber = 1;

    public int WorkerCount { get; } = Environment.ProcessorCount;

    public Shop(string stockOption)
    {
        FillStock(stockOption);

        for (int i = 1; i <= WorkerCount; i++)
        {
            int id = i;
            new Thread(() => WorkerLoop(id)) { IsBackground = true }.Start();
        }

        new Thread(SaveLoop) { IsBackground = true }.Start();
        _stockChanged.Set();
    }

    public string Menu()
    {
        lock (_lock)
            return "MENU " + string.Join(", ", _products.Select(p => $"{p.Name}={p.Stock} ({p.Seconds}s)"));
    }

    public bool TryParseItems(string[] words, out Dictionary<Product, long> items, out string error)
    {
        items = new Dictionary<Product, long>();
        error = "";

        if (words.Length < 3 || words.Length % 2 == 0)
        {
            error = $"usage: {words[0].ToUpperInvariant()} <product> <quantity> [<product> <quantity> ...]";
            return false;
        }

        for (int i = 1; i < words.Length; i += 2)
        {
            string name = words[i].ToLowerInvariant();
            Product? product = Array.Find(_products, p => p.Name == name);
            if (product == null)
            {
                error = $"unknown product '{Shorten(words[i])}' (we sell coffee, tea, croissant)";
                return false;
            }

            if (!int.TryParse(words[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int quantity) || quantity <= 0)
            {
                error = $"the quantity of {product.Name} must be a positive number";
                return false;
            }

            items[product] = items.GetValueOrDefault(product) + quantity;
        }
        return true;
    }

    public static string Shorten(string text) => text.Length <= 24 ? text : text[..24] + "...";

    // Check and take happen under one lock.
    public void PlaceOrder(Connection client, Dictionary<Product, long> items)
    {
        var logLines = new List<string>();

        lock (_lock)
        {
            var missing = items.Where(item => item.Value > item.Key.Stock)
                               .Select(item => $"{item.Key.Name}: asked {item.Value}, left {item.Key.Stock}")
                               .ToList();

            if (missing.Count > 0)
            {
                string reason = string.Join("; ", missing);
                client.Send("REFUSED " + reason);
                logLines.Add("order refused: " + reason);
            }
            else
            {
                foreach (var (product, quantity) in items)
                {
                    bool wasEnough = product.Stock >= LowStock;
                    product.Stock -= quantity;
                    if (wasEnough && product.Stock < LowStock)
                        logLines.Add(AlertProvider(product));
                }

                var order = new Order(_nextNumber++, client, items);
                _activeOrders.Add(order);

                // ACCEPTED goes first, READY cannot overtake.
                client.Send($"ACCEPTED order={order.Number} time={order.Seconds}s");
                _queue.Add(order);
                logLines.Add($"order #{order.Number} accepted: {Describe(items)}, {order.Seconds} s");
            }
        }

        foreach (string line in logLines)
            Log.Write(client.Endpoint, line);
    }

    public void Deliver(Connection provider, Dictionary<Product, long> items)
    {
        string logLine;

        lock (_lock)
        {
            if (items.Any(item => item.Key.Stock + item.Value > MaxStock))
            {
                logLine = $"error: delivery over the stock limit of {MaxStock}";
                provider.Send($"ERROR delivery refused: the stock limit is {MaxStock}");
            }
            else
            {
                foreach (var (product, quantity) in items)
                    product.Stock += quantity;

                string result = string.Join(", ", items.Select(item => $"{item.Key.Name} +{item.Value} = {item.Key.Stock}"));
                provider.Send("DELIVERED " + result);
                logLine = "delivery: " + result;
                _stockChanged.Set();
            }
        }

        Log.Write(provider.Endpoint, logLine);
    }

    // A late provider gets every current alert.
    public bool TrySetProvider(Connection connection)
    {
        var low = new List<string>();

        lock (_lock)
        {
            if (_provider != null)
                return false;

            _provider = connection;
            connection.Send("OK role=provider");
            foreach (Product product in _products.Where(p => p.Stock < LowStock))
            {
                connection.Send(AlertText(product));
                low.Add($"alert sent: {product.Name} has {product.Stock} left");
            }
        }

        foreach (string line in low)
            Log.Write(connection.Endpoint, line);
        return true;
    }

    public void RemoveProvider(Connection connection)
    {
        lock (_lock)
        {
            if (_provider == connection)
                _provider = null;
        }
    }

    // Every unfinished order gives its products back.
    public void CancelOrders(Connection client)
    {
        var logLines = new List<string>();

        lock (_lock)
        {
            foreach (Order order in _activeOrders.Where(o => o.Client == client).ToList())
            {
                string where = order.State == OrderState.Preparing ? "while preparing" : "while waiting";
                order.State = OrderState.Cancelled;
                foreach (var (product, quantity) in order.Items)
                    product.Stock += quantity;

                _activeOrders.Remove(order);
                order.Cancelled.Set();
                logLines.Add($"order #{order.Number} cancelled {where}, products back in stock");
            }
        }

        foreach (string line in logLines)
            Log.Write(client.Endpoint, line);
    }

    // Free workers take the oldest order.
    private void WorkerLoop(int id)
    {
        foreach (Order order in _queue.GetConsumingEnumerable())
        {
            try
            {
                Prepare(order, id);
            }
            catch (Exception ex)
            {
                Log.Write($"worker {id}", $"error on order #{order.Number}: {ex.Message}");
            }
        }
    }

    private void Prepare(Order order, int workerId)
    {
        lock (_lock)
        {
            if (order.State != OrderState.Waiting)
                return;
            order.State = OrderState.Preparing;
        }
        Log.Write(order.Client.Endpoint, $"order #{order.Number} preparing on worker {workerId}");

        // Waits the preparation time, or wakes when cancelled.
        if (order.Cancelled.Wait(TimeSpan.FromSeconds(order.Seconds)))
        {
            Log.Write(order.Client.Endpoint, $"order #{order.Number} stopped on worker {workerId}");
            return;
        }

        lock (_lock)
        {
            if (order.State != OrderState.Preparing)
                return;
            order.State = OrderState.Ready;
            _activeOrders.Remove(order);
            order.Client.Send($"READY order={order.Number}");
            _stockChanged.Set();
        }
        Log.Write(order.Client.Endpoint, $"order #{order.Number} ready");
    }

    // Must run with the lock held.
    private string AlertProvider(Product product)
    {
        if (_provider == null)
            return $"alert kept for the provider: {product.Name} has {product.Stock} left";

        _provider.Send(AlertText(product));
        return $"alert sent to provider {_provider.Endpoint}: {product.Name} has {product.Stock} left";
    }

    private static string AlertText(Product product) => $"ALERT {product.Name} is low: {product.Stock} left";

    private static string Describe(Dictionary<Product, long> items) =>
        string.Join(", ", items.Select(item => $"{item.Value} {item.Key.Name}"));

    private void FillStock(string option)
    {
        if (option == "saved" && LoadStock())
        {
            Log.Write("shop", $"stock loaded from {_stockFile}");
            return;
        }

        foreach (Product product in _products)
            product.Stock = option is "saved" or "random" ? Random.Shared.Next(10, 51) : int.Parse(option);
    }

    private bool LoadStock()
    {
        try
        {
            if (!File.Exists(_stockFile))
                return false;

            var saved = new Dictionary<string, long>();
            foreach (string line in File.ReadAllLines(_stockFile))
            {
                string[] parts = line.Split('=');
                if (parts.Length != 2 || !long.TryParse(parts[1], out long amount) || amount < 0)
                    return false;
                saved[parts[0]] = Math.Min(amount, MaxStock);
            }

            if (!_products.All(p => saved.ContainsKey(p.Name)))
                return false;

            foreach (Product product in _products)
                product.Stock = saved[product.Name];
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // File writing stays outside the lock.
    private void SaveLoop()
    {
        string tempFile = _stockFile + ".tmp";

        while (true)
        {
            _stockChanged.WaitOne();

            string text;
            lock (_lock)
            {
                // Unfinished orders count as stock after a crash.
                text = string.Join("\n", _products.Select(p =>
                    $"{p.Name}={p.Stock + _activeOrders.Sum(o => o.Items.GetValueOrDefault(p))}"));
            }

            try
            {
                File.WriteAllText(tempFile, text);
                File.Move(tempFile, _stockFile, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Write("shop", $"error: could not save the stock: {ex.Message}");
            }
        }
    }
}
