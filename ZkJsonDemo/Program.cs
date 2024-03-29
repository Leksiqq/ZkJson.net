﻿using Net.Leksi.ZkJson;
using org.apache.zookeeper;
using org.apache.zookeeper.data;
using System.Text.Json;
using ZkJsonDemo;

const string s_successUpdate = "Requested subtree was updated successfully!";
const string s_successRead = "\nRequested subtree was read successfully!";
const string s_successDelete = "Requested subtree was deleted successfully!";
const string s_connecting = "Connecting to ZooKeeper '{0}' ...";
const string s_zkState = "ZooKeeper is {0}";
const string s_failed = "failed!";
const string s_pathNotExists = "Requested subtree does not exist!";

Options? options = Options.Create(args);
if (options is null)
{
    Environment.Exit(1);
    return;
}

ZooKeeper zk = null!;
ManualResetEventSlim mres = new(false);

try
{
    Console.WriteLine(s_connecting, options.ConnectionString);
    zk = new ZooKeeper(options.ConnectionString, options.Timeout, new ZKWatcher(mres));
    mres.Wait(options.Timeout);

    Console.WriteLine(s_zkState, zk.getState());

    if (zk.getState() is not ZooKeeper.States.CONNECTED)
    {
        Console.WriteLine(s_failed);
        Environment.Exit(1);
        return;
    }

    ZkJsonSerializer factory = new() 
    {
        ZooKeeper = zk,
        Root = options.Path,
    };

    JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true,
    };
    serializerOptions.Converters.Add(factory);

    if (options.Reader is { })
    {
        if (options.Update)
        {
            factory.Action = ZkAction.Update;
        }
        await JsonSerializer.DeserializeAsync<ZkStub>(options.Reader, serializerOptions);
        Console.WriteLine(s_successUpdate);
    }
    else if (options.Writer is { })
    {
        if (await zk.existsAsync("/") is Stat stat)
        {

            if (options.BasePropertyName is { })
            {
                await JsonSerializer.SerializeAsync(options.Writer, factory.IncrementalSerialize(options.BasePropertyName), serializerOptions);
            }
            else
            {
                await JsonSerializer.SerializeAsync(options.Writer, ZkStub.Instance, serializerOptions);
            }
            Console.WriteLine(s_successRead);
        }
        else
        {
            Console.WriteLine(s_pathNotExists);
            Environment.Exit(1);
            return;
        }
    }
    else if (options.Delete)
    {
        if (await zk.existsAsync("/") is Stat stat)
        {
            await factory.DeleteAsync();
            Console.WriteLine(s_successDelete);
        }
        else
        {
            Console.WriteLine(s_pathNotExists);
            Environment.Exit(1);
            return;
        }
    }

}
catch (JsonException ex)
{
    switch (ex.HResult)
    {
        case ZkJsonException.IncrementalCycle:
            Console.WriteLine($"{ex.GetType()}: {ex.Message}");
            Console.WriteLine("Hops:");
            foreach (string s in (List<string>)ex.Data[nameof(ZkJsonException.IncrementalCycle)]!)
            {
                Console.WriteLine($"    {s}");
            }
            Console.WriteLine();
            Console.WriteLine(ex.StackTrace);
            break;
        default:
            Console.WriteLine(ex);
            break;
    }
}
finally
{
    options.Reader?.Close();
    options.Writer?.Close();
}
