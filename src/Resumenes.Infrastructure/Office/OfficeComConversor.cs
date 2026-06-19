using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Office;

/// <summary>
/// Conversor de respaldo que usa Microsoft Office (Word/PowerPoint/Excel) vía COM, si está
/// instalado. Sirve de fallback cuando LibreOffice falla. Usa <c>late binding</c> (no requiere
/// los ensamblados de Interop ni Office en tiempo de compilación): si Office no está, lanza una
/// excepción clara y el orquestador decide.
/// </summary>
[SupportedOSPlatform("windows")]
public class OfficeComConversor : IConversorOffice
{
    /// <summary>True si hay alguna aplicación de Office registrada (Word) en la máquina.</summary>
    public static bool OfficeDisponible =>
        Type.GetTypeFromProgID("Word.Application") is not null
        || Type.GetTypeFromProgID("PowerPoint.Application") is not null
        || Type.GetTypeFromProgID("Excel.Application") is not null;

    public Task<string> ConvertirAPdfAsync(string archivoOffice, string outDir, CancellationToken ct)
    {
        // COM de Office requiere un apartamento STA; el pipeline corre en hilos del pool (MTA).
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var th = new Thread(() =>
        {
            try { tcs.SetResult(Convertir(archivoOffice, outDir)); }
            catch (Exception ex) { tcs.SetException(ex); }
        })
        { IsBackground = true, Name = "OfficeComConversor" };
        th.SetApartmentState(ApartmentState.STA);
        th.Start();
        using (ct.Register(() => { /* la app COM se cierra en el finally; no se puede abortar el hilo */ }))
        {
            return tcs.Task;
        }
    }

    private static string Convertir(string archivoOffice, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var ext = Path.GetExtension(archivoOffice).ToLowerInvariant();
        var pdf = Path.Combine(outDir, Path.GetFileNameWithoutExtension(archivoOffice) + ".pdf");

        switch (ext)
        {
            case ".doc" or ".docx" or ".rtf" or ".odt" or ".txt":
                ConvertirWord(archivoOffice, pdf);
                break;
            case ".ppt" or ".pptx" or ".odp":
                ConvertirPowerPoint(archivoOffice, pdf);
                break;
            case ".xls" or ".xlsx" or ".ods" or ".csv":
                ConvertirExcel(archivoOffice, pdf);
                break;
            default:
                throw new NotSupportedException($"Office (COM) no soporta la extensión '{ext}'.");
        }

        if (!File.Exists(pdf))
            throw new InvalidOperationException("Microsoft Office no generó el PDF.");
        return pdf;
    }

    private static dynamic CrearApp(string progId)
    {
        var t = Type.GetTypeFromProgID(progId);
        if (t is null)
            throw new InvalidOperationException($"Microsoft Office no disponible ({progId} no registrado).");
        return Activator.CreateInstance(t)
            ?? throw new InvalidOperationException($"No se pudo iniciar {progId}.");
    }

    private static void ConvertirWord(string archivo, string pdf)
    {
        dynamic? app = null, doc = null;
        try
        {
            app = CrearApp("Word.Application");
            app.Visible = false;
            app.DisplayAlerts = 0; // wdAlertsNone
            doc = app.Documents.Open(archivo, ReadOnly: true, AddToRecentFiles: false);
            doc.ExportAsFixedFormat(pdf, 17); // WdExportFormat.wdExportFormatPDF
        }
        finally
        {
            try { if (doc is not null) { doc.Close(false); Marshal.FinalReleaseComObject(doc); } } catch { }
            try { if (app is not null) { app.Quit(false); Marshal.FinalReleaseComObject(app); } } catch { }
        }
    }

    private static void ConvertirPowerPoint(string archivo, string pdf)
    {
        // PowerPoint no permite Visible=false (lanza); se abre sin ventana.
        dynamic? app = null, pres = null;
        try
        {
            app = CrearApp("PowerPoint.Application");
            pres = app.Presentations.Open(archivo, ReadOnly: -1, Untitled: -1, WithWindow: 0); // MsoTriState
            pres.SaveAs(pdf, 32); // PpSaveAsFileType.ppSaveAsPDF
        }
        finally
        {
            try { if (pres is not null) { pres.Close(); Marshal.FinalReleaseComObject(pres); } } catch { }
            try { if (app is not null) { app.Quit(); Marshal.FinalReleaseComObject(app); } } catch { }
        }
    }

    private static void ConvertirExcel(string archivo, string pdf)
    {
        dynamic? app = null, wb = null;
        try
        {
            app = CrearApp("Excel.Application");
            app.Visible = false;
            app.DisplayAlerts = false;
            wb = app.Workbooks.Open(archivo, ReadOnly: true);
            wb.ExportAsFixedFormat(0, pdf); // XlFixedFormatType.xlTypePDF
        }
        finally
        {
            try { if (wb is not null) { wb.Close(false); Marshal.FinalReleaseComObject(wb); } } catch { }
            try { if (app is not null) { app.Quit(); Marshal.FinalReleaseComObject(app); } } catch { }
        }
    }
}
