// Lab 2: a product of the shop and one order.
namespace Lab2;

public sealed class Product
{
    public string Name { get; }
    public int Seconds { get; }
    public long Stock { get; set; }

    public Product(string name, int seconds)
    {
        Name = name;
        Seconds = seconds;
    }
}

public enum OrderState { Waiting, Preparing, Ready, Cancelled }

public sealed class Order
{
    public int Number { get; }
    public Connection Client { get; }
    public Dictionary<Product, long> Items { get; }
    public long Seconds { get; }
    public OrderState State { get; set; } = OrderState.Waiting;

    // Wakes the worker when the client disappears.
    public ManualResetEventSlim Cancelled { get; } = new(false);

    public Order(int number, Connection client, Dictionary<Product, long> items)
    {
        Number = number;
        Client = client;
        Items = items;
        foreach (var (product, quantity) in items)
            Seconds += product.Seconds * quantity;
    }
}
