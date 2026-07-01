using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Application.Services;
using BexioOrderImport.Application.Options;
using BexioOrderImport.Infrastructure.Excel;
using BexioOrderImport.Infrastructure.Bexio;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Cli;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        // Show Logo and Intro
        AnsiConsole.Write(new FigletText("Bexio Importer").Color(Color.DeepPink3));
        AnsiConsole.MarkupLine("[grey]Textil-Excel-Bestellformular Import-Schnittstelle[/]\n");

        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Fehler:[/] Bitte geben Sie den Pfad zu einer Excel-Bestellungsdatei als Argument an.");
            AnsiConsole.MarkupLine("[yellow]Verwendung:[/] BexioOrderImport.Cli.exe <Pfad-zu-Excel-Datei>");
            return;
        }

        string filePath = args[0];
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Die angegebene Excel-Datei existiert nicht: [white]{Path.GetFullPath(filePath)}[/]");
            return;
        }

        // Setup Host (DI & Config)
        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Register Excel mapping options
                services.Configure<ExcelMappingOptions>(context.Configuration.GetSection("ExcelMapping"));
                services.AddSingleton<IExcelParser, ClosedXmlExcelParser>();
                
                // Read Bexio configurations
                var bexioToken = context.Configuration["Bexio:ApiToken"] ?? "YOUR_TOKEN";
                int accountId = int.Parse(context.Configuration["Bexio:DefaultAccountId"] ?? "3200");
                int taxId = int.Parse(context.Configuration["Bexio:DefaultTaxId"] ?? "1");

                services.AddHttpClient<IBexioClient, BexioApiClient>()
                    .AddTypedClient<IBexioClient>(httpClient => 
                        new BexioApiClient(httpClient, bexioToken, accountId, taxId));

                services.AddSingleton<ImportOrderUseCase>();
            })
            .Build();

        var useCase = host.Services.GetRequiredService<ImportOrderUseCase>();

        try
        {
            await useCase.ExecuteAsync(
                filePath,
                showPreviewCallback: ShowOrderPreview,
                confirmUploadCallback: ConfirmUploadPromptAsync,
                confirmCustomerCreationCallback: ConfirmCustomerCreationPromptAsync,
                logInfoCallback: message => 
                {
                    if (message.Contains("Warnung") || message.Contains("warning"))
                    {
                        AnsiConsole.MarkupLine($"[yellow][[warn]][/] {message}");
                    }
                    else if (message.Contains("Erfolgreich"))
                    {
                        AnsiConsole.MarkupLine($"[green][[ok]][/] {message}");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[blue][[info]][/] {message}");
                    }
                }
            );
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }
    }

    private static void ShowOrderPreview(Order order)
    {
        AnsiConsole.Write(new Rule("[yellow]Vorschau der extrahierten Bestellung[/]").LeftJustified());

        // Header Informationen ausgeben
        var headerTable = new Table().Border(TableBorder.Rounded);
        headerTable.AddColumn("[bold]Feld[/]");
        headerTable.AddColumn("[bold]Excel-Wert[/]");
        headerTable.AddRow("Kunde / Firma", order.Customer.CompanyName);
        headerTable.AddRow("Adresse", $"{order.Customer.Street}, {order.Customer.ZipCode} {order.Customer.City}");
        headerTable.AddRow("E-Mail", order.Customer.Email);
        headerTable.AddRow("Einkäufer", order.Customer.BuyerName);
        headerTable.AddRow("Liefertermin", order.DeliveryDate?.ToString("dd.MM.yyyy") ?? "Nicht definiert");
        headerTable.AddRow("Konditionen", order.PaymentTerms);
        AnsiConsole.Write(headerTable);

        // Positionstabelle ausgeben
        var posTable = new Table().Border(TableBorder.Square);
        posTable.AddColumn("[bold]Art. Nr.[/]");
        posTable.AddColumn("[bold]Bezeichnung[/]");
        posTable.AddColumn("[bold]Farbe[/]");
        posTable.AddColumn("[bold]Kategorie[/]");
        posTable.AddColumn("[bold]Grösse[/]");
        posTable.AddColumn(new TableColumn("[bold]Menge[/]").RightAligned());
        posTable.AddColumn(new TableColumn("[bold]EP[/]").RightAligned());
        posTable.AddColumn(new TableColumn("[bold]Betrag[/]").RightAligned());

        foreach (var pos in order.Positions)
        {
            posTable.AddRow(
                pos.ArticleNumber,
                pos.ArticleName,
                pos.Color,
                pos.SizeCategory,
                pos.Size,
                pos.Quantity.ToString(),
                pos.UnitPrice.ToString("F2"),
                pos.TotalPrice.ToString("F2")
            );
        }

        if (order.DiscountPercent > 0)
        {
            posTable.Caption = new TableTitle($"Gesamtsumme Artikel: [bold green]{order.TotalQuantity}[/] Stück   |   Bruttobetrag: [bold yellow]{order.TotalAmount:F2} CHF[/]   |   Rabatt: [bold red]{order.DiscountPercent}%[/]   |   Nettobetrag (exkl. MwSt.): [bold green]{order.TotalNetAmount:F2} CHF[/]");
        }
        else
        {
            posTable.Caption = new TableTitle($"Gesamtsumme Artikel: [bold green]{order.TotalQuantity}[/] Stück   |   Total Betrag (exkl. MwSt.): [bold green]{order.TotalAmount:F2} CHF[/]");
        }
        AnsiConsole.Write(posTable);
    }

    private static async Task<bool> ConfirmUploadPromptAsync()
    {
        Console.WriteLine();
        return AnsiConsole.Confirm("Möchten Sie diese Bestellung jetzt an Bexio übermitteln?");
    }

    private static async Task<bool> ConfirmCustomerCreationPromptAsync(Customer customer)
    {
        Console.WriteLine();
        AnsiConsole.MarkupLine($"[yellow][[warn]][/] Kunde mit der E-Mail [bold]{customer.Email}[/] wurde in Bexio nicht gefunden.");
        
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold]Feld[/]");
        table.AddColumn("[bold]Wert für neuen Kunden[/]");
        table.AddRow("Firma / Name", customer.CompanyName);
        table.AddRow("Strasse", customer.Street);
        table.AddRow("PLZ Ort", $"{customer.ZipCode} {customer.City}");
        table.AddRow("E-Mail", customer.Email);
        AnsiConsole.Write(table);
        
        return AnsiConsole.Confirm("Möchten Sie einen neuen Kunden mit diesen Informationen in Bexio anlegen?");
    }
}
