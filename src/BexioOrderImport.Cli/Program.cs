using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Application.Options;
using BexioOrderImport.Application.Services;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Infrastructure.Bexio;
using BexioOrderImport.Infrastructure.Excel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace BexioOrderImport.Cli;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Show Logo and Intro
        AnsiConsole.Write(new FigletText("Bexio Importer").Color(Color.DeepPink3));
        AnsiConsole.MarkupLine("[grey]Textile Excel Order Form Import Interface[/]\n");

        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Please specify the path to an Excel order file as an argument.");
            AnsiConsole.MarkupLine("[yellow]Usage:[/] BexioOrderImport.Cli.exe <path-to-excel-file>");
            return;
        }

        string filePath = args[0];
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] The specified Excel file does not exist: [white]{Path.GetFullPath(filePath)}[/]");
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
                    if (message.Contains("Warning") || message.Contains("warning"))
                    {
                        AnsiConsole.MarkupLine($"[yellow][[warn]][/] {message}");
                    }
                    else if (message.Contains("Successfully") || message.Contains("Success"))
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
        AnsiConsole.Write(new Rule("[yellow]Preview of Extracted Order[/]").LeftJustified());

        // Display Header info
        var headerTable = new Table().Border(TableBorder.Rounded);
        headerTable.AddColumn("[bold]Field[/]");
        headerTable.AddColumn("[bold]Excel Value[/]");
        headerTable.AddRow("Customer / Company", order.Customer.CompanyName);
        headerTable.AddRow("Address", $"{order.Customer.Street}, {order.Customer.ZipCode} {order.Customer.City}");
        headerTable.AddRow("E-Mail", order.Customer.Email);
        headerTable.AddRow("Buyer Name", order.Customer.BuyerName);
        headerTable.AddRow("Date", order.DeliveryDate?.ToString("yyyy-MM-dd") ?? "Not defined");
        headerTable.AddRow("Terms", order.PaymentTerms);
        AnsiConsole.Write(headerTable);

        // Display Positions Table
        var posTable = new Table().Border(TableBorder.Square);
        posTable.AddColumn("[bold]Art No.[/]");
        posTable.AddColumn("[bold]Description[/]");
        posTable.AddColumn("[bold]Color[/]");
        posTable.AddColumn("[bold]Category[/]");
        posTable.AddColumn("[bold]Size[/]");
        posTable.AddColumn(new TableColumn("[bold]Quantity[/]").RightAligned());
        posTable.AddColumn(new TableColumn("[bold]Unit Price[/]").RightAligned());
        posTable.AddColumn(new TableColumn("[bold]Total Price[/]").RightAligned());

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
            posTable.Caption = new TableTitle($"Total Quantity: [bold green]{order.TotalQuantity}[/] pcs   |   Gross Total: [bold yellow]{order.TotalAmount:F2} CHF[/]   |   Discount: [bold red]{order.DiscountPercent}%[/]   |   Net Total (excl. VAT): [bold green]{order.TotalNetAmount:F2} CHF[/]");
        }
        else
        {
            posTable.Caption = new TableTitle($"Total Quantity: [bold green]{order.TotalQuantity}[/] pcs   |   Total Price (excl. VAT): [bold green]{order.TotalAmount:F2} CHF[/]");
        }
        AnsiConsole.Write(posTable);
    }

    private static async Task<bool> ConfirmUploadPromptAsync()
    {
        Console.WriteLine();
        return AnsiConsole.Confirm("Do you want to upload this order to Bexio now?");
    }

    private static async Task<bool> ConfirmCustomerCreationPromptAsync(Customer customer)
    {
        Console.WriteLine();
        AnsiConsole.MarkupLine($"[yellow][[warn]][/] Customer with e-mail [bold]{customer.Email}[/] was not found in Bexio.");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold]Field[/]");
        table.AddColumn("[bold]New Customer Value[/]");
        table.AddRow("Company / Name", customer.CompanyName);
        table.AddRow("Street", customer.Street);
        table.AddRow("ZIP City", $"{customer.ZipCode} {customer.City}");
        table.AddRow("E-Mail", customer.Email);
        AnsiConsole.Write(table);

        return AnsiConsole.Confirm("Do you want to create a new customer in Bexio with this information?");
    }
}
