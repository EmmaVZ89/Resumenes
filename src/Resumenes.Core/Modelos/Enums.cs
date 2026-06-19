namespace Resumenes.Core.Modelos;

public enum Etapa
{
    Captura,
    OcrBruto,
    LimpiezaIA,
    ConsolidacionTemas,
    ResumenFinal,
    GeneracionPDF
}

public enum EstadoUnidad { Pendiente, EnProceso, Completado, Error, Obsoleto }

public enum EstadoAnalisis { EnProceso, Completado, ConErrores, Obsoleto }

public enum TipoArchivo { Pdf, Doc, Docx, Ppt, Pptx, Txt, Otro }
