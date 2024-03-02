using System.Diagnostics;
using MiniXML;

Console.WriteLine("Press any key to start parsing XML from file.");
Console.ReadKey(true);

Element result = default!;

long elapsedTime;

using (var fs = File.OpenRead(@".\Dummy.xml"))
{
    Console.CursorVisible = false;
    Console.WriteLine("  -> Parsing...\n");

    using (var parser = new Parser(fs))
    {
        parser.OnStreamElement += e =>
        {
            result = e;
        };

        var sw = Stopwatch.StartNew();

        while (!parser.IsEndOfStream)
            parser.Update();

        elapsedTime = sw.ElapsedMilliseconds;
    }
}

Console.CursorVisible = true;

Console.WriteLine("\nParsing completed! Took " + elapsedTime + "ms\n\n");
Console.ReadKey(true);

Console.WriteLine(result.ToString());
Console.ReadKey(true);