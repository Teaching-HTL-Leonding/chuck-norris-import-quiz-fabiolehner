﻿using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

//Ich habe Hilfe von Maximilian Pineker angenommen

var factory = new FactContextFactory();
await using var dbContext = factory.CreateDbContext();

if (args.Length < 1)
{
    await Add();
}
else if ("clear".Equals(args[0]))
{
    await dbContext.Database.ExecuteSqlRawAsync("DELETE");
}
else
{
    var count = int.Parse(args[0]);

    if (count > 10)
    {
        Console.WriteLine("There can't be more than 10 jokes imported");
        return;
    }
    if (count < 1)
    {
        await Add(count);
    }
}   

async Task<Fact> GetFact()
{
    const string url = @"https://api.chucknorris.io/jokes/random";
    var request = (HttpWebRequest)WebRequest.Create(url);
    string json;
    request.AutomaticDecompression = DecompressionMethods.GZip;

    using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
    await using (Stream stream = response.GetResponseStream())
    using (StreamReader reader = new(stream))
    {
        json = await reader.ReadToEndAsync();
    }

    return JsonSerializer.Deserialize<Fact>(json);
}



async Task Add(int count = 5)
{
    var factory = new FactContextFactory();
    await using var dbContext = factory.CreateDbContext();
    await using var transaction = await dbContext.Database.BeginTransactionAsync();
    try
    {
        for (var i = 0; i < count; i++)
        {
            var fact = await (GetFact() ?? throw new NullReferenceException());
            if (!dbContext.Facts.Contains(fact))
            {
                await dbContext.Facts.AddAsync(fact);
                Console.WriteLine($"{fact.Id}\n{fact.ChuckNorrisId}\n{fact.Url}\n{fact.Joke}\n");
            }
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch (Exception e)
    {
        await transaction.RollbackAsync();
        Console.WriteLine(e);
        throw;
    }
}


//Table werden created
public class Fact
{
    //Automatisch generierte Id
    public int Id { get; set; }


    [JsonPropertyName("id")]
    [MaxLength(40)]
    public string ChuckNorrisId { get; set; } = string.Empty;


    [JsonPropertyName("url")]
    [MaxLength(1024)]
    public string Url { get; set; } = string.Empty;


    [JsonPropertyName("value")]
    public string Joke { get; set; } = string.Empty;
}




//Methoden aus Cheatsheet
public class FactContext : DbContext
{
    public FactContext(DbContextOptions<FactContext> options)
        : base(options)
    {
    }

    public DbSet<Fact> Facts { get; set; } = null!;
}

public class FactContextFactory : IDesignTimeDbContextFactory<FactContext>
{
    public FactContext CreateDbContext(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<FactContext>();
        optionsBuilder
            // Uncomment the following line if you want to print generated
            // SQL statements on the console.
            .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
            .UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new FactContext(optionsBuilder.Options);
    }
}