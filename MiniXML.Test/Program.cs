using MiniXML;

Console.WriteLine("Press any key to start parsing XML from file.");
Console.ReadKey(true);

Element result = default!;

double elapsedTime;

using (var fs = File.OpenRead(@".\Dummy.xml"))
{
    Console.CursorVisible = false;
    Console.WriteLine("  -> Parsing...\n");
    var lastTime = DateTime.Now;
    result = Xml.Parse(fs);
    elapsedTime = (DateTime.Now - lastTime).TotalMilliseconds;
}

Console.CursorVisible = true;

Console.WriteLine($"\nParsing completed! Took {elapsedTime:F2}ms\n\n");
Console.ReadKey(true);

Console.WriteLine(result.ToString());
Console.ReadKey(true);