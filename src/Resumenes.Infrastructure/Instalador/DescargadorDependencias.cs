using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Instalador;

public class DescargadorDependencias(HttpClient http, string manifestUrl, string raizRuntime) : IDescargadorDependencias
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<ResultadoDescarga> DescargarFaltantesAsync(IProgress<EstadoDescarga>? progreso, CancellationToken ct)
    {
        progreso?.Report(new EstadoDescarga("", FaseDescarga.LeyendoManifest, 0, 0, 0, 0, "Leyendo manifest…"));
        var json = (await http.GetStringAsync(manifestUrl, ct)).TrimStart('﻿');
        var manifest = JsonSerializer.Deserialize<ManifestDescarga>(json, JsonOpts)
            ?? throw new InvalidOperationException("Manifest inválido o vacío.");

        int ok = 0, salt = 0, err = 0;
        var fallos = new List<string>();
        var bundles = manifest.Bundles ?? new();

        for (int i = 0; i < bundles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var b = bundles[i];
            var destino = ResolverDestino(b.Destino);
            int idx = i + 1, total = bundles.Count;
            try
            {
                if (YaInstalado(destino, b.Sha256))
                {
                    salt++;
                    progreso?.Report(new EstadoDescarga(b.Id, FaseDescarga.Completado, b.Bytes, b.Bytes, idx, total, "Ya instalado"));
                    continue;
                }

                var zipPath = destino + ".part";
                await DescargarConReanudacionAsync(b, zipPath, idx, total, progreso, ct);

                progreso?.Report(new EstadoDescarga(b.Id, FaseDescarga.Verificando, b.Bytes, b.Bytes, idx, total, "Verificando…"));
                var hash = await Sha256Async(zipPath, ct);
                if (!string.Equals(hash, b.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(zipPath);
                    throw new InvalidOperationException($"SHA-256 no coincide (esperado {b.Sha256}, obtenido {hash}).");
                }

                progreso?.Report(new EstadoDescarga(b.Id, FaseDescarga.Descomprimiendo, b.Bytes, b.Bytes, idx, total, "Descomprimiendo…"));
                if (b.LimpiarDestino && Directory.Exists(destino)) Directory.Delete(destino, true);
                Directory.CreateDirectory(destino);
                ZipFile.ExtractToDirectory(zipPath, destino, overwriteFiles: true);
                File.Delete(zipPath);
                File.WriteAllText(Path.Combine(destino, ".bundle-ok"), b.Sha256);

                ok++;
                progreso?.Report(new EstadoDescarga(b.Id, FaseDescarga.Completado, b.Bytes, b.Bytes, idx, total, "Listo"));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                err++;
                fallos.Add($"{b.Id}: {ex.Message}");
                progreso?.Report(new EstadoDescarga(b.Id, FaseDescarga.Error, 0, b.Bytes, idx, total, ex.Message));
            }
        }
        return new ResultadoDescarga(ok, salt, err, fallos);
    }

    private string ResolverDestino(string destino)
    {
        var exp = Environment.ExpandEnvironmentVariables(destino ?? "");
        return Path.IsPathRooted(exp) ? exp : Path.Combine(raizRuntime, exp);
    }

    private static bool YaInstalado(string destino, string sha)
    {
        var marcador = Path.Combine(destino, ".bundle-ok");
        return File.Exists(marcador) &&
               string.Equals(File.ReadAllText(marcador).Trim(), sha, StringComparison.OrdinalIgnoreCase);
    }

    private async Task DescargarConReanudacionAsync(BundleDescarga b, string zipPath, int idx, int total,
        IProgress<EstadoDescarga>? progreso, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
        long yaTengo = File.Exists(zipPath) ? new FileInfo(zipPath).Length : 0;
        // Reanudar SOLO si conocemos el tamaño total (bytes>0) y el parcial es válido (menor que el total).
        // Si el tamaño es desconocido o el .part quedó sobredimensionado/corrupto, empezar de cero.
        bool puedeReanudar = b.Bytes > 0 && yaTengo > 0 && yaTengo < b.Bytes;
        if (!puedeReanudar)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            yaTengo = 0;
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, b.Url);
        if (yaTengo > 0) req.Headers.Range = new RangeHeaderValue(yaTengo, null);

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        bool reanuda = resp.StatusCode == HttpStatusCode.PartialContent && yaTengo > 0;
        if (!reanuda) yaTengo = 0;
        resp.EnsureSuccessStatusCode();

        await using var entrada = await resp.Content.ReadAsStreamAsync(ct);
        await using var salida = new FileStream(zipPath, reanuda ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long descargado = yaTengo;
        int n;
        while ((n = await entrada.ReadAsync(buffer, ct)) > 0)
        {
            await salida.WriteAsync(buffer.AsMemory(0, n), ct);
            descargado += n;
            progreso?.Report(new EstadoDescarga(b.Id, FaseDescarga.Descargando, descargado, b.Bytes, idx, total,
                $"{descargado / 1_048_576} / {(b.Bytes > 0 ? b.Bytes / 1_048_576 : 0)} MB"));
        }
    }

    private static async Task<string> Sha256Async(string path, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var fs = File.OpenRead(path);
        return Convert.ToHexString(await sha.ComputeHashAsync(fs, ct));
    }
}

internal record ManifestDescarga(string Version, List<BundleDescarga> Bundles);
internal record BundleDescarga(string Id, string Url, string Sha256, long Bytes, string Destino, string Tipo, bool LimpiarDestino = true);
