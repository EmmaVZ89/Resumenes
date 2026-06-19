#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
generador_estudio_final.py
==========================
Motor de estilo FIJO para generar los PDF de estudio de la app de Resumenes.

Diferencias clave frente al generador_estudio.py original (pensado para producción):

1) CONTENIDO POR ARCHIVO, NO HARDCODEADO.
   El original tenía bloques `if "puertos" in path ... elif "argentina" ...` con texto
   incrustado. Acá TODO el contenido (incluidas ampliaciones, ejemplos, datos y tips)
   viene del archivo de entrada que produce la IA. Agregar un análisis nuevo NO requiere
   tocar este código.

2) FORMATO DE ENTRADA ESTRUCTURADO Y DETERMINISTA (en vez de adivinar con heurísticas).
   La IA emite marcadores explícitos; el parser es predecible. Igual hay fallback tolerante
   (prosa suelta -> párrafo, "- " -> viñeta, "## " -> sección) para que "nada falle".

3) SIN fix_orthography.
   La ortografía (tildes, ñ) es responsabilidad de la IA (limpieza/resumen) y del UTF-8
   punta a punta. Un diccionario de reemplazos es frágil y puede introducir errores.

4) FUENTES EMPAQUETADAS.
   DejaVu NO viene instalada por defecto en Windows. Se buscan en (en orden):
   $RESUMENES_FONTS, ./fonts junto a este script, y C:/Windows/Fonts como último recurso.
   En el instalable, las .ttf se empaquetan en ./fonts.

5) ESCRITURA ATÓMICA (.tmp + os.replace), igual que el original.

------------------------------------------------------------------------------------------
FORMATO DE ENTRADA (.txt / .md, UTF-8)
------------------------------------------------------------------------------------------
Metadatos (en cualquier parte, una por línea):
    #TITULO: GEOGRAFIA ECONOMICA MUNDIAL
    #SUBTITULO: Conceptos base y redes
    #FUENTE: Resumen generado a partir del material del alumno

Marcadores de contenido (uno por línea; \\n dentro del texto = salto de línea):
    @seccion: Título de la sección
    @texto: Un párrafo normal.
    @blt: Un punto con viñeta
    @ejemplo: Texto del recuadro EJEMPLO
    @dato: Texto del recuadro DATO CLAVE
    @tip: Texto del recuadro PARA ESTUDIAR
    @pagina            (fuerza salto de página)

Fallback tolerante (si la IA no usa marcadores):
    "## algo"  -> sección       "- algo" o "• algo" -> viñeta
    línea en MAYÚSCULAS (>3)     -> sección
    cualquier otra línea no vacía -> párrafo

------------------------------------------------------------------------------------------
USO
------------------------------------------------------------------------------------------
    python generador_estudio_final.py entrada.txt salida.pdf
    python generador_estudio_final.py entrada.txt salida.pdf --titulo "..." --subtitulo "..."

O como librería (compatible con los wrappers generar_0X.py):
    from generador_estudio_final import build_pdf
    build_pdf("entrada.txt", "salida.pdf", "TITULO", "Subtitulo")
"""

import os
import re
import sys
import argparse
from fpdf import FPDF

# --- Iconos Unicode (compatibles con DejaVu) ---
ICO_EARTH = "☉"
ICO_BOOK = "▶"
ICO_BULLET = "•"
ICO_ARROW = "▸"
ICO_DIAMOND = "◆"
ICO_CHECK = "✓"
ICO_STAR = "★"


def _resolver_font_dir():
    """Devuelve el primer directorio que contenga DejaVuSans.ttf."""
    candidatos = []
    env = os.environ.get("RESUMENES_FONTS")
    if env:
        candidatos.append(env)
    candidatos.append(os.path.join(os.path.dirname(os.path.abspath(__file__)), "fonts"))
    candidatos.append("C:/Windows/Fonts")
    for d in candidatos:
        if d and os.path.exists(os.path.join(d, "DejaVuSans.ttf")):
            return d
    raise RuntimeError(
        "No se encontraron las fuentes DejaVu (DejaVuSans*.ttf). "
        "Empaquetalas en la carpeta ./fonts junto a este script "
        "o definí la variable de entorno RESUMENES_FONTS. "
        "DejaVu es de redistribución libre."
    )


class StudyPDF(FPDF):
    def __init__(self, title, subtitle, source_label):
        super().__init__()
        self.title = title
        self.subtitle = subtitle
        self.source_label = source_label
        self.set_auto_page_break(auto=True, margin=18)

        # Paleta
        self.c_title = (30, 80, 200)
        self.c_sub = (0, 130, 120)
        self.c_section = (100, 50, 180)
        self.c_example = (230, 130, 0)
        self.c_data = (200, 40, 40)
        self.c_tip = (40, 140, 60)
        self.c_bg_section = (237, 231, 246)
        self.c_bg_example = (255, 243, 224)
        self.c_bg_data = (227, 242, 253)
        self.c_bg_tip = (232, 245, 233)
        self.c_text = (40, 40, 40)
        self.c_gray = (110, 110, 110)

        font_dir = _resolver_font_dir()
        self.add_font("DV", "", os.path.join(font_dir, "DejaVuSans.ttf"))
        self.add_font("DV", "B", os.path.join(font_dir, "DejaVuSans-Bold.ttf"))
        self.add_font("DV", "I", os.path.join(font_dir, "DejaVuSans-Oblique.ttf"))
        self.add_font("DV", "BI", os.path.join(font_dir, "DejaVuSans-BoldOblique.ttf"))
        self.fn = "DV"

    def header(self):
        if self.page_no() > 1:
            self.set_font(self.fn, "I", 7)
            self.set_text_color(*self.c_gray)
            self.cell(130, 7, f"{ICO_EARTH} {self.title}", align="L")
            self.cell(55, 7, f"Pagina {self.page_no()}", align="R", new_x="LMARGIN", new_y="NEXT")
            self.set_draw_color(200, 200, 200)
            self.set_line_width(0.3)
            self.line(10, 15, 200, 15)
            self.ln(4)

    def footer(self):
        self.set_y(-14)
        self.set_font(self.fn, "I", 6.5)
        self.set_text_color(*self.c_gray)
        self.cell(0, 8, self.source_label, align="C")

    def portada(self):
        self.add_page()
        self.ln(18)
        self.set_draw_color(*self.c_title)
        self.set_line_width(1.4)
        self.line(28, self.get_y(), 182, self.get_y())
        self.ln(5)
        self.set_font(self.fn, "B", 28)
        self.set_text_color(*self.c_title)
        self.cell(0, 14, f"{ICO_EARTH}  {self.title}", align="C", new_x="LMARGIN", new_y="NEXT")
        self.ln(3)
        self.set_font(self.fn, "B", 15)
        self.set_text_color(*self.c_sub)
        self.cell(0, 9, self.subtitle, align="C", new_x="LMARGIN", new_y="NEXT")
        self.ln(6)
        self.set_draw_color(*self.c_sub)
        self.set_line_width(0.8)
        self.line(58, self.get_y(), 152, self.get_y())
        self.ln(10)

    def ensure_space(self, needed):
        if self.get_y() + needed > 275:
            self.add_page()

    def section(self, title):
        self.ensure_space(20)
        self.ln(3)
        y0 = self.get_y()
        lines = self.multi_cell(174, 5.5, title, dry_run=True, output="LINES")
        h = max(len(lines) * 5.5 + 8, 15)
        self.set_fill_color(*self.c_bg_section)
        self.rect(10, y0, 190, h, style="F")
        self.set_fill_color(*self.c_section)
        self.rect(10, y0, 3, h, style="F")
        self.set_xy(16, y0 + 3)
        self.set_font(self.fn, "B", 10)
        self.set_text_color(*self.c_section)
        self.multi_cell(176, 5.5, f"{ICO_STAR} {title}", new_x="LMARGIN", new_y="NEXT")
        self.set_y(y0 + h + 2)

    def paragraph(self, text):
        self.ensure_space(10)
        self.set_font(self.fn, "", 9)
        self.set_text_color(*self.c_text)
        self.multi_cell(0, 5.2, text, new_x="LMARGIN", new_y="NEXT")
        self.ln(1)

    def bullet(self, text, color=None):
        self.ensure_space(8)
        self.set_font(self.fn, "", 9)
        self.set_text_color(*(color or self.c_text))
        self.cell(6, 5.2, ICO_ARROW)
        self.multi_cell(0, 5.2, text, new_x="LMARGIN", new_y="NEXT")

    def box(self, title, content, bg, border, title_color):
        self.ln(2)
        y0 = self.get_y()
        self.set_font(self.fn, "", 8.7)
        lines = self.multi_cell(172, 5, content, dry_run=True, output="LINES")
        h = len(lines) * 5 + 14
        if y0 + h > 280:
            self.add_page()
            y0 = self.get_y()
        self.set_fill_color(*bg)
        self.rect(12, y0, 186, h, style="F")
        self.set_fill_color(*border)
        self.rect(12, y0, 3, h, style="F")
        self.set_xy(19, y0 + 3)
        self.set_font(self.fn, "B", 8)
        self.set_text_color(*title_color)
        self.cell(0, 5, title, new_x="LMARGIN", new_y="NEXT")
        self.set_x(19)
        self.set_font(self.fn, "", 8.7)
        self.set_text_color(*self.c_text)
        self.multi_cell(172, 5, content, new_x="LMARGIN", new_y="NEXT")
        self.set_y(y0 + h + 2)

    def example(self, content):
        self.box(f"{ICO_CHECK} EJEMPLO", content, self.c_bg_example, self.c_example, self.c_example)

    def data(self, content):
        self.box(f"{ICO_DIAMOND} DATO CLAVE", content, self.c_bg_data, self.c_data, self.c_data)

    def tip(self, content):
        self.box(f"{ICO_BOOK} PARA ESTUDIAR", content, self.c_bg_tip, self.c_tip, self.c_tip)


# --- Parser del formato estructurado ---

_META_RE = re.compile(r"^#(TITULO|SUBTITULO|FUENTE)\s*:\s*(.*)$", re.IGNORECASE)
_MARK_RE = re.compile(r"^@(\w+)\s*:?\s?(.*)$")


def _desescapar(texto):
    """Convierte la secuencia literal \\n en salto de línea real."""
    return texto.replace("\\n", "\n")


def parse_contenido(lines):
    """Devuelve (meta:dict, bloques:list[(tipo, contenido)])."""
    meta = {}
    bloques = []
    for raw in lines:
        line = raw.rstrip("\n")
        s = line.strip()
        if not s:
            continue

        m = _META_RE.match(s)
        if m:
            meta[m.group(1).lower()] = m.group(2).strip()
            continue

        m = _MARK_RE.match(s)
        if m:
            key = m.group(1).lower()
            content = _desescapar(m.group(2).strip())
            if key in ("pagina", "nuevapagina"):
                bloques.append(("pagina", ""))
            elif key in ("seccion", "section"):
                bloques.append(("seccion", content))
            elif key in ("texto", "txt", "parrafo"):
                bloques.append(("texto", content))
            elif key in ("blt", "bullet", "vineta"):
                bloques.append(("bullet", content))
            elif key in ("ejemplo", "example"):
                bloques.append(("ejemplo", content))
            elif key in ("dato", "data"):
                bloques.append(("dato", content))
            elif key in ("tip", "recordar"):
                bloques.append(("tip", content))
            else:
                # Marcador desconocido -> párrafo (robustez)
                bloques.append(("texto", content))
            continue

        # --- Fallback tolerante (sin marcadores) ---
        if s.startswith("## "):
            bloques.append(("seccion", s[3:].strip()))
        elif s.startswith("- ") or s.startswith("• "):
            bloques.append(("bullet", s[2:].strip()))
        elif s.startswith("# "):
            bloques.append(("seccion", s[2:].strip()))
        elif s.isupper() and len(s) > 3:
            bloques.append(("seccion", s))
        else:
            bloques.append(("texto", s))

    return meta, bloques


def render_bloques(pdf, bloques):
    despacho = {
        "seccion": pdf.section,
        "texto": pdf.paragraph,
        "bullet": pdf.bullet,
        "ejemplo": pdf.example,
        "dato": pdf.data,
        "tip": pdf.tip,
    }
    for tipo, contenido in bloques:
        if tipo == "pagina":
            pdf.add_page()
        elif tipo in despacho and contenido:
            despacho[tipo](contenido)


def build_pdf(txt_path, out_path, title=None, subtitle=None):
    """Genera el PDF de estudio a partir de un archivo de contenido estructurado.

    Firma compatible con los wrappers generar_0X.py existentes.
    """
    with open(txt_path, "r", encoding="utf-8") as f:
        lines = [ln.rstrip("\n") for ln in f]

    meta, bloques = parse_contenido(lines)

    # Prioridad: argumento > metadato del archivo > primera línea no vacía
    primera = next((l.strip() for l in lines if l.strip()), "Material de estudio")
    title = title or meta.get("titulo") or primera
    subtitle = subtitle or meta.get("subtitulo") or "Guia de estudio"
    fuente = meta.get("fuente") or f"Fuente: {os.path.basename(txt_path)}"

    pdf = StudyPDF(title, subtitle, fuente)
    pdf.portada()
    render_bloques(pdf, bloques)

    # Escritura atómica (D3): escribir a .tmp y reemplazar
    tmp_out = out_path + ".tmp.pdf"
    pdf.output(tmp_out)
    try:
        os.replace(tmp_out, out_path)
    except PermissionError:
        # El destino podría estar abierto (PDF en pantalla); no perder el resultado
        alt_out = re.sub(r"\.pdf$", "_nuevo.pdf", out_path, flags=re.IGNORECASE)
        os.replace(tmp_out, alt_out)
        return alt_out
    return out_path


def _main(argv=None):
    p = argparse.ArgumentParser(description="Genera un PDF de estudio desde un archivo de contenido estructurado.")
    p.add_argument("entrada", help="Ruta del .txt/.md de contenido (UTF-8)")
    p.add_argument("salida", help="Ruta del .pdf de salida")
    p.add_argument("--titulo", default=None)
    p.add_argument("--subtitulo", default=None)
    args = p.parse_args(argv)
    destino = build_pdf(args.entrada, args.salida, args.titulo, args.subtitulo)
    print(f"PDF generado: {destino}")
    return 0


if __name__ == "__main__":
    sys.exit(_main())
