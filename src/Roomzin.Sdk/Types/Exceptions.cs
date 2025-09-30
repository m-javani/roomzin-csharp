// RoomzinException.cs  (NET 6+, C# 10+)
using System;

namespace Roomzin.Sdk.Types
{
    public enum ErrorKind : byte
    {
        Client = 0,
        Request = 1,
        Internal = 2,
        Retry = 3
    }

    /// <summary>
    /// Base exception that carries the <see cref="Kind"/> and the original
    /// code string.  This is the C# counterpart of *RoomzinError.
    /// </summary>
    public class RoomzinException : Exception
    {
        public ErrorKind Kind { get; }
        public string Code { get; }

        public RoomzinException(ErrorKind kind, string code, string message)
            : base($"{code}:{message}")
        {
            Kind = kind;
            Code = code;
        }

        /* ---- helpers that mimic the Go predicates ---- */
        public static bool IsClient(Exception? ex) => ex is RoomzinException re && re.Kind == ErrorKind.Client;
        public static bool IsRequest(Exception? ex) => ex is RoomzinException re && re.Kind == ErrorKind.Request;
        public static bool IsInternal(Exception? ex) => ex is RoomzinException re && re.Kind == ErrorKind.Internal;
        public static bool IsRetry(Exception? ex) => ex is RoomzinException re && re.Kind == ErrorKind.Retry;

        /* ---- factory that mimics RzError(string|Exception, kind?) ---- */
        public static RoomzinException From(object input, ErrorKind? kind = null)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));

            // 1. Already wrapped – return as-is
            if (input is RoomzinException re) return re;

            string raw = input switch
            {
                Exception ex => ex.Message ?? ex.GetType().Name,
                _ => input.ToString()!
            };

            // 2. Split into code:message (or take whole string)
            string code, msg;
            int idx = raw.IndexOf(':');
            if (idx > 0)
            {
                code = raw[..idx];
                msg = raw[(idx + 1)..];
            }
            else
            {
                code = raw;
                msg = raw;
            }

            // 3. Use supplied kind, or classify from code, or fall back to INTERNAL
            ErrorKind k = kind ?? ClassifyCode(code);

            // 4. Build final message
            string finalMsg = $"{code}:{msg}";
            return new RoomzinException(k, code, finalMsg);
        }

        /* ---------- same classification table as Java ---------- */
        private static ErrorKind ClassifyCode(string code)
        {
            return code.ToUpperInvariant() switch
            {
                "AUTH_ERROR" or "CLIENT_ERROR" => ErrorKind.Client,
                "VALIDATION_ERROR" or "NOT_FOUND" or "OVERFLOW" or
                "UNDERFLOW" or "FORBIDDEN" or "REQUEST_ERROR" => ErrorKind.Request,
                "429" or "503" or "308" or "405" or "RETRY_ERROR" => ErrorKind.Retry,
                _ => ErrorKind.Internal
            };
        }
    }

    /* ------------------------------------------------------------------
     * Public concrete exceptions – unchanged public shape.
     * ------------------------------------------------------------------ */

    public sealed class ValidationException(string message) : RoomzinException(ErrorKind.Request, "VALIDATION_ERROR", message)
    {
    }

}