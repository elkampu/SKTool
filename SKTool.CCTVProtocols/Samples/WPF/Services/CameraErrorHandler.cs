using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Windows;
using SKTool.CCTVProtocols.Hikvision;

namespace SKTool.CCTVProtocols.Samples.WPF.Services
{
    public static class CameraErrorHandler
    {
        public static void Handle(Exception ex, string? context = null)
        {
            ex = Unwrap(ex);

            var title = string.IsNullOrWhiteSpace(context) ? "Camera Error" : $"Camera Error - {context}";
            var sb = new StringBuilder();

            switch (ex)
            {
                case HikvisionIsapiException hx:
                    sb.AppendLine($"HTTP {hx.HttpStatus}: {hx.Message}");
                    if (!string.IsNullOrWhiteSpace(hx.RawBody))
                    {
                        sb.AppendLine();
                        sb.AppendLine(hx.RawBody);
                    }
                    break;

                case TaskCanceledException:
                case OperationCanceledException:
                    sb.Append("Request timed out or was canceled.");
                    break;

                case AuthenticationException:
                    sb.Append("TLS/SSL handshake failed. Check HTTPS setting and device certificate.");
                    break;

                case HttpRequestException hre:
                    if (hre.StatusCode is { } sc)
                        sb.Append($"Network error {(int)sc} {sc}: {hre.Message}");
                    else
                        sb.Append($"Network error: {hre.Message}");
                    if (hre.InnerException is AuthenticationException)
                        sb.Append("\nTLS/SSL handshake failed. Check HTTPS setting and device certificate.");
                    if (hre.InnerException is SocketException se1)
                        sb.Append($"\nSocket error: {se1.SocketErrorCode} (0x{se1.NativeErrorCode:X})");
                    break;

                case SocketException se:
                    sb.Append($"Socket error: {se.SocketErrorCode} (0x{se.NativeErrorCode:X}).");
                    break;

                case TimeoutException:
                    sb.Append("Operation timed out.");
                    break;

                default:
                    sb.Append(ex.Message);
                    break;
            }

            MessageBox.Show(sb.ToString(), title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static Exception Unwrap(Exception ex)
        {
            if (ex is AggregateException ae && ae.InnerExceptions.Count == 1)
                return Unwrap(ae.InnerException!);
            return ex;
        }
    }
}