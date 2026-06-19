#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Worker OCR de larga vida (PaddleOCR 3.x). Protocolo NDJSON por stdin/stdout.
Uso: worker_ocr.py <modelos_dir>. Lee {"req_id","ruta_imagen"} por linea y responde
{"type":"result"|"error"|"ready", ...}."""
import os, sys, json

# Modelos cacheados localmente: evitar el chequeo de conectividad (puede colgar el init; debe ser offline).
os.environ.setdefault("PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK", "True")

from paddleocr import PaddleOCR


def emit(obj):
    sys.stdout.write(json.dumps(obj, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def extraer_lineas(resultado):
    """Robusto a PaddleOCR 2.x (lista [box,(text,conf)]) y 3.x (OCRResult con 'rec_texts')."""
    lineas = []
    for res in (resultado or []):
        textos = res.get("rec_texts") if isinstance(res, dict) else getattr(res, "rec_texts", None)
        if textos is not None:
            lineas.extend(t for t in textos if t)
            continue
        try:
            for item in (res or []):
                lineas.append(item[1][0])
        except Exception:
            pass
    return lineas


def reconocer(ocr, ruta):
    metodo = getattr(ocr, "predict", None) or ocr.ocr
    return metodo(ruta)


def main():
    # enable_mkldnn=False evita el crash de oneDNN/PIR en builds CPU de Windows
    # (NotImplementedError ConvertPirAttribute2RuntimeAttribute en onednn_instruction.cc).
    try:
        ocr = PaddleOCR(lang="es", use_textline_orientation=True, enable_mkldnn=False)
    except TypeError:
        ocr = PaddleOCR(lang="es", use_textline_orientation=True)
    emit({"type": "ready"})

    # readline() (en vez de "for line in sys.stdin") evita el read-ahead buffering que
    # retiene la linea de pedido y cuelga el protocolo interactivo por pipe.
    while True:
        line = sys.stdin.readline()
        if line == "":          # EOF: el orquestador cerro stdin
            break
        line = line.lstrip("﻿").strip()   # descartar BOM si el emisor lo agrego
        if not line:
            continue
        try:
            req = json.loads(line)
            rid, ruta = req["req_id"], req["ruta_imagen"]
        except Exception as ex:
            emit({"type": "error", "req_id": None, "mensaje": f"peticion invalida: {ex}"})
            continue
        try:
            lineas = extraer_lineas(reconocer(ocr, ruta))
            emit({"type": "result", "req_id": rid, "texto": "\n".join(lineas)})
        except Exception as ex:
            emit({"type": "error", "req_id": rid, "mensaje": str(ex)})


if __name__ == "__main__":
    main()
