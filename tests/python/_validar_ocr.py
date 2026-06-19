#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Validación independiente del camino OCR (sin IA, sin API key):
crea un PDF de prueba con tildes/ñ, lo rasteriza con PyMuPDF y lo OCR-ea con PaddleOCR 3.x.
Descarga los modelos la primera vez (lento). Imprime la estructura del resultado y el texto."""
import os, sys, tempfile

# PaddlePaddle/oneDNN crashea en algunos builds CPU de Windows (onednn_instruction.cc).
# Desactivar oneDNN evita ese camino. Debe setearse ANTES de importar paddle.
os.environ.setdefault("FLAGS_use_mkldnn", "0")

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FIX_DIR = os.path.join(ROOT, "tests", "fixtures", "prueba")
FIX = os.path.join(FIX_DIR, "con_tildes.pdf")
FONT = os.path.join(ROOT, "runtime", "fonts", "DejaVuSans.ttf")

def crear_fixture():
    os.makedirs(FIX_DIR, exist_ok=True)
    if os.path.exists(FIX):
        return
    from fpdf import FPDF
    pdf = FPDF()
    pdf.add_page()
    pdf.add_font("DV", "", FONT)
    pdf.set_font("DV", size=20)
    pdf.multi_cell(0, 14,
        "Comercio exterior y logistica.\n"
        "El nandu come piniones en la region.\n"
        "Areas: produccion, distribucion y transicion.\n"
        "Acentos de prueba: canon, accion, mas alla.")
    pdf.output(FIX)
    print("fixture creado:", FIX, flush=True)

def main():
    crear_fixture()
    import fitz
    imgdir = tempfile.mkdtemp(prefix="ocrval_")
    doc = fitz.open(FIX)
    imgs = []
    for i, page in enumerate(doc, 1):
        pix = page.get_pixmap(dpi=200, colorspace=fitz.csGRAY)
        p = os.path.join(imgdir, f"p{i:04d}.jpg")
        pix.save(p); imgs.append(p)
    print("imagenes:", len(imgs), imgs[0], flush=True)

    from paddleocr import PaddleOCR
    print("inicializando PaddleOCR (puede descargar modelos)...", flush=True)
    try:
        ocr = PaddleOCR(lang="es", use_textline_orientation=True, enable_mkldnn=False)
        print("ctor con enable_mkldnn=False", flush=True)
    except TypeError as e:
        print("enable_mkldnn no aceptado:", e, flush=True)
        ocr = PaddleOCR(lang="es", use_textline_orientation=True)
    metodo = getattr(ocr, "predict", None) or ocr.ocr
    res = metodo(imgs[0])
    print("TIPO_RESULTADO:", type(res).__name__, flush=True)
    primero = res[0] if res else None
    print("TIPO_ELEMENTO:", type(primero).__name__, flush=True)
    if isinstance(primero, dict):
        print("CLAVES:", list(primero.keys()), flush=True)

    # misma extracción que worker_ocr.py
    lineas = []
    for r in (res or []):
        t = r.get("rec_texts") if isinstance(r, dict) else getattr(r, "rec_texts", None)
        if t:
            lineas.extend(x for x in t if x)
    print("TEXTO_OCR:")
    print("\n".join(lineas) if lineas else "(vacío)", flush=True)

if __name__ == "__main__":
    main()
