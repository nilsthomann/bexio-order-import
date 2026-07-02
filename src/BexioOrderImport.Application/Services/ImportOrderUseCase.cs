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

    public async Task<bool> ExecuteAsync(
        string filePath, 
        Action<Order> showPreviewCallback, 
        Func<Task<bool>> confirmUploadCallback,
        Func<Customer, Task<bool>> confirmCustomerCreationCallback,
        Action<string> logInfoCallback)
    {
        logInfoCallback($"Reading Excel file: {Path.GetFileName(filePath)}...");
        var order = _excelParser.ParseOrderForm(filePath);
        
        if (order.Positions.Count == 0)
        {
            logInfoCallback("No order positions with quantity > 0 found.");
            return false;
        }

        // 1. Show preview
        showPreviewCallback(order);

        // 2. Ask confirmation
        bool confirmed = await confirmUploadCallback();
        if (!confirmed)
        {
            logInfoCallback("Order import cancelled.");
            return false;
        }

        // 3. Start API upload
        logInfoCallback("Connecting to Bexio API...");
        int? contactId = await _bexioClient.FindContactIdAsync(order.Customer.Email);
        if (!contactId.HasValue)
        {
            bool createCustomerConfirmed = await confirmCustomerCreationCallback(order.Customer);
            if (!createCustomerConfirmed)
            {
                logInfoCallback("Order import cancelled (customer was not created).");
                return false;
            }
            logInfoCallback("Creating new customer in Bexio...");
            contactId = await _bexioClient.CreateContactAsync(order.Customer);
        }
        logInfoCallback($"Customer matched (Bexio ID: {contactId.Value}). Creating order...");
        
        int orderId = await _bexioClient.CreateOrderAsync(contactId.Value, order);
        logInfoCallback($"Order created successfully (Bexio ID: {orderId}). Uploading positions...");

        int count = 0;
        foreach (var pos in order.Positions)
        {
            // Try to find article in Bexio
            var articleId = await _bexioClient.FindArticleIdAsync(pos.ArticleNumber, pos.ArticleName);
            if (articleId.HasValue)
            {
                await _bexioClient.AddArticlePositionAsync(orderId, articleId.Value, pos);
            }
            else
            {
                logInfoCallback($"[yellow]Warning:[/] Article '{pos.ArticleNumber}' ({pos.ArticleName}) not found in Bexio. Creating custom position...");
                await _bexioClient.AddCustomPositionAsync(orderId, pos);
            }

            count++;
            if (count % 5 == 0 || count == order.Positions.Count)
            {
                logInfoCallback($"Positions uploaded: {count}/{order.Positions.Count}");
            }
        }

        logInfoCallback($"Successfully completed! Order #{orderId} has been imported into Bexio.");
        return true;
    }
}
