using System;
using System.IO;
using System.Threading.Tasks;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Application.Services;

public class ImportOrderUseCase
{
    private readonly IExcelParser _excelParser;
    private readonly IBexioClient _bexioClient;

    public ImportOrderUseCase(IExcelParser excelParser, IBexioClient bexioClient)
    {
        _excelParser = excelParser;
        _bexioClient = bexioClient;
    }

    public async Task ExecuteAsync(
        string filePath, 
        Action<Order> showPreviewCallback, 
        Func<Task<bool>> confirmUploadCallback,
        Func<Customer, Task<bool>> confirmCustomerCreationCallback,
        Action<string> logInfoCallback)
    {
        logInfoCallback($"Lese Excel-Datei ein: {Path.GetFileName(filePath)}...");
        var order = _excelParser.ParseOrderForm(filePath);
        
        if (order.Positions.Count == 0)
        {
            logInfoCallback("Keine Bestellpositionen mit Menge > 0 gefunden.");
            return;
        }

        // 1. Vorschau anzeigen
        showPreviewCallback(order);

        // 2. Sicherheitsabfrage
        bool confirmed = await confirmUploadCallback();
        if (!confirmed)
        {
            logInfoCallback("Import vom Auftrag abgebrochen.");
            return;
        }

        // 3. API Upload starten
        logInfoCallback("Verbindung mit Bexio API wird aufgebaut...");
        int? contactId = await _bexioClient.FindContactIdAsync(order.Customer.Email);
        if (!contactId.HasValue)
        {
            bool createCustomerConfirmed = await confirmCustomerCreationCallback(order.Customer);
            if (!createCustomerConfirmed)
            {
                logInfoCallback("Import vom Auftrag abgebrochen (Kunde wurde nicht erstellt).");
                return;
            }
            logInfoCallback("Erstelle neuen Kunden in Bexio...");
            contactId = await _bexioClient.CreateContactAsync(order.Customer);
        }
        logInfoCallback($"Kunde zugeordnet (Bexio ID: {contactId.Value}). Erstelle Auftrag...");
        
        int orderId = await _bexioClient.CreateOrderAsync(contactId.Value, order);
        logInfoCallback($"Auftrag erfolgreich erstellt (Bexio ID: {orderId}). Lade Positionen hoch...");

        int count = 0;
        foreach (var pos in order.Positions)
        {
            // Versuche Artikel in Bexio zu finden
            var articleId = await _bexioClient.FindArticleIdAsync(pos.ArticleNumber, pos.ArticleName);
            if (articleId.HasValue)
            {
                await _bexioClient.AddArticlePositionAsync(orderId, articleId.Value, pos);
            }
            else
            {
                logInfoCallback($"[yellow]Warnung:[/] Artikel '{pos.ArticleNumber}' ({pos.ArticleName}) nicht in Bexio gefunden. Erstelle Freiposition...");
                await _bexioClient.AddCustomPositionAsync(orderId, pos);
            }

            count++;
            if (count % 5 == 0 || count == order.Positions.Count)
            {
                logInfoCallback($"Positionen hochgeladen: {count}/{order.Positions.Count}");
            }
        }

        logInfoCallback($"Erfolgreich abgeschlossen! Auftrag #{orderId} wurde in Bexio importiert.");
    }
}
