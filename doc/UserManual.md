# Bexio Order Importer – User Manual

Welcome to the **Bexio Order Importer** user manual. This document provides a comprehensive guide on how to configure, run, and utilize the application to automate your Excel order processing and import data directly into the Bexio ERP system.

---

## Table of Contents
1. [Introduction](#1-introduction)
2. [Prerequisites & Installation](#2-prerequisites--installation)
3. [Configuration & Initial Setup](#3-configuration--initial-setup)
4. [Understanding the Excel Order Template](#4-understanding-the-excel-order-template)
5. [Step-by-Step Import Guide](#5-step-by-step-import-guide)
6. [Updating the Application](#6-updating-the-application)
7. [Troubleshooting & Support](#7-troubleshooting--support)

---

## 1. Introduction

The **Bexio Order Importer** is a tool designed to bridge the gap between traditional Excel-based order sheets (frequently used in wholesale, fashion, or retail distribution) and the Bexio REST API. 

It parses complex spreadsheet files containing customer headers, delivery dates, special discounts, and matrix-based size/quantity layouts, and automatically:
- Checks if the customer exists in Bexio (by email).
- Prompts you to confirm or create a new contact in Bexio.
- Generates a draft order with all mapped article positions.

---

## 2. Prerequisites & Installation

### System Requirements
- **Operating System**: Windows 10 or Windows 11 (64-bit).
- **Runtime**: .NET Runtime 10.0 (automatically managed or installed by the system).

### Installation via Installer
1. Download the latest `BexioOrderImportSetup.exe` from the **GitHub Releases** page.
2. Run the installer.
3. The installer operates as a low-privilege user install, meaning **no administrator rights (UAC prompt) are required**.
4. The application will install to your local user directory (`%LocalAppData%\Programs\BexioOrderImport`) and automatically create Start Menu and Desktop shortcuts.

---

## 3. Configuration & Initial Setup

When you first launch the application, you must configure the Bexio API connection and mapping parameters. Go to the **Einstellungen** (Settings) tab in the sidebar.

![Settings Panel Preview](assets/excel_template_preview.png)
*(See section 4 below for mapping detailed coordinates matching the order sheet)*

### Bexio API Settings
1. **API-Token**: Paste your Bexio API Token here. To protect your credentials, this field automatically masks the token with dots (`••••••••`) when it loses focus. Clicking inside the field immediately shows the actual text for editing.
   - *How to get a token*: Log into your Bexio account, navigate to **Settings > Apps & API > API Keys** (Einstellungen > Apps & API > API-Schlüssel) and generate a new token.
2. **Standard-Konto (Default Account)**: The default bookkeeping account number (e.g., `3200` for product sales) used for custom order positions.
3. **Standard-MwSt (Default Tax ID)**: The default tax rate index mapped in your Bexio account (typically `1` for the standard domestic tax rate).

> [!NOTE]
> All configuration files are securely saved in your local user directory at `%LocalAppData%\BexioOrderImport\appsettings.json`. They remain fully preserved when the application updates itself to a newer version.

---

## 4. Understanding the Excel Order Template

The importer is built to parse complex order forms that list article rows horizontally but expand size quantities vertically (as a matrix). 

### Example Sheet Coordinates
Below is a typical order template layout showing how the parser reads the metadata and positions:

![Excel Template Layout Preview](assets/excel_template_preview.png)

#### Mapped Excel Fields
* **Customer Header Metadata**:
  * **Firma (Company)** [Cell `B4`]: Mapped customer company name.
  * **Strasse (Street)** [Cell `B5`]: Mapped customer street name.
  * **PLZ Ort (ZIP & City)** [Cell `B6`]: Formatted as `[ZIP] [City]` (e.g., `7000 Chur`). The parser automatically splits this cell into separate postal code and city values.
  * **E-Mail** [Cell `E5`]: Used to search for existing contacts in Bexio.
  * **Einkauf (Buyer Name)** [Cell `E4`]: Mapped primary contact person.
  * **Liefertermin (Delivery Date)** [Cell `T7`]: Date of planned delivery.
  * **Zahlungskonditionen (Payment Terms)** [Cell `A9`]: Plain text terms of payment.
  * **Rabatt (Global Discount %)** [Cell `V12`]: The general discount rate applied to the whole order.
* **Size Matrix Mapping**:
  * **Start/End Row** [Rows `10` to `17`]: Coordinates defining where the size headers are declared.
  * **Category Column** [Column `4` / D]: Defines which category matches which size column.
  * **Start/End Size Column** [Columns `5` (E) to `18` (R)]: Tells the parser which range of columns holds size definitions (e.g., size `20`, `21`, `22`, etc.).
* **Data Row Mapping**:
  * **Start Row** [Row `18`]: Defines where the actual article lists begin.
  * **Article Number** [Column `1` / A]: Mapped product identifier (SKU).
  * **Article Name** [Column `2` / B]: Product description.
  * **Color** [Column `3` / C]: Product color.
  * **Category** [Column `4` / D]: Product category matching the matrix headers.
  * **UnitPrice (EP)** [Column `20` / T]: Individual wholesale price.

---

## 5. Step-by-Step Import Guide

### Step 1: Loading the Order Sheet
1. Navigate to the **Import** tab in the sidebar.
2. Select your Excel file (`.xlsx`) using one of two methods:
   - **Drag and Drop**: Drag the Excel sheet directly onto the dashed upload card.
   - **File Browser**: Click the upload card to open the Windows file explorer and select the file.
3. The application will display a rotating spinner while parsing.

### Step 2: Review Mapped Data & Preview
1. The **Kopfdaten** (Header Details) card displays the extracted customer metadata, delivery dates, and payment conditions.
2. The **Positions preview grid** displays all parsed item rows. Only sizes with a quantity greater than zero will generate a row.
3. You can double-click cells in the DataGrid to manually override quantities or prices before uploading.
4. The bottom totals bar dynamically reflects the gross amount, discounts, and net total.

### Step 3: Trigger the Import
1. Click **Import starten** (Start Import) in the bottom right corner.
2. **Customer Checking**:
   - The application checks your Bexio database for a contact matching the customer's email.
   - **If found**: It proceeds automatically using that contact's ID.
   - **If NOT found**: A dialog pops up showing the new customer details. You can review, correct errors (e.g., misspelled street names), and click **Erstellen** to insert them directly into Bexio. If you click **Abbrechen** (Cancel), the import is aborted and the file remains loaded in your view.
3. **Confirm Upload**:
   - A final confirm dialog asks if you want to push the order to Bexio.
   - Click **Ja** (Yes) to upload.
4. During upload, a full-screen loading card covers the interface. Once completed, a success dialog displays the created Bexio order number.

---

## 6. Updating the Application

The application checks for updates automatically from its GitHub repository on every launch. 

1. If a newer version is available, a colored **Update Banner** appears at the top of the interface:
   * *„Ein Update auf Version v1.1.0 ist verfügbar.“*
2. Click **Jetzt installieren** (Install Now).
3. The app displays a progress bar while downloading the setup bundle to your temp folder.
4. The app will then run the installer silently in the background, close itself automatically to release file locks, update your installation, and restart. Your profile settings will remain untouched.

---

## 7. Troubleshooting & Support

### Common Issues

#### 1. Bexio Connection shows a Red Status Light
- **Check API Token**: Go to the **Einstellungen** tab, click inside the token field to display it in plaintext, and verify that it matches your Bexio API Key exactly.
- **Firewall/Network**: Make sure your computer has an active internet connection and can reach `https://api.bexio.com`.

#### 2. The Excel Parsing Fails / throws a Coordinates Error
- **Verify Mapping**: Ensure the cell coordinates listed in your settings match your sheet exactly. Note that columns must be written as **numbers** (e.g., Column E is `5`, Column T is `20`) whereas individual header cells are written as standard **cell coordinates** (e.g., `B4`).
- **Sheet Index**: Ensure your Worksheet Index corresponds to the sheet containing the order (usually `1` for the first tab).

#### 3. Created Order has "Custom Positions" instead of Catalog Articles
- The importer queries your Bexio database for an article code matching the Excel `Artikel Nr.` column.
- If the article number is not found in your Bexio product catalog, the importer automatically inserts the row as a custom/free position using the product name, price, and color so that your import doesn't fail. To map to catalog products, make sure the article codes match your Bexio catalog exactly.
