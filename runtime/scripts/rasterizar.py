#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Rasteriza un PDF a JPG por pagina. Uso: rasterizar.py <pdf> <out_dir> <dpi>
Imprime una ruta de imagen por linea (stdout)."""
import sys, os
import fitz  # PyMuPDF

def main():
    pdf_path, out_dir, dpi = sys.argv[1], sys.argv[2], int(sys.argv[3])
    os.makedirs(out_dir, exist_ok=True)
    doc = fitz.open(pdf_path)
    for i, page in enumerate(doc, start=1):
        pix = page.get_pixmap(dpi=dpi, colorspace=fitz.csGRAY)
        ruta = os.path.join(out_dir, f"pagina_{i:04d}.jpg")
        pix.save(ruta)
        print(ruta, flush=True)

if __name__ == "__main__":
    main()
