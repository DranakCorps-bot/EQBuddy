using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EQBuddy.UI.Shared;

/// <summary>
/// Rename a file OVER a live name so the name never resolves to nothing — the one operation
/// <see cref="WholeFilePublish"/> needs and the one the BCL does not expose (DRA-257).
///
/// <para><b>Both obvious answers were measured and both are wrong</b>, which is the whole
/// reason this file exists rather than a one-line call:</para>
///
/// <list type="number">
/// <item><description><c>File.Move(pending, path, overwrite: true)</c> —
/// <c>MoveFileEx(MOVEFILE_REPLACE_EXISTING | MOVEFILE_COPY_ALLOWED)</c>. Not atomic: there is
/// an instant in which the destination name resolves to NOTHING. On a 2-core
/// <c>ProcessorAffinity</c> mask this was red <b>3 runs in 8</b>, with bursts of 259–951
/// absent-name reads in ~75,000 and <c>zeroByteFile=0 partialContent=0</c> every time. This is
/// what shipped from DRA-228 and what DRA-255 caught.</description></item>
///
/// <item><description><c>File.Replace(pending, path, null, ignoreMetadataErrors: true)</c> —
/// Win32 <c>ReplaceFile</c>, which DRA-257's card named as the candidate because it is the one
/// usually called "the atomic replace". <b>Measured, it is far worse:</b> red <b>12 runs in
/// 12</b>, ~25% of all reads torn (e.g. <c>torn reads: 62708 of 222472 — absentName=31670
/// openRefused=31038</c>). It opens the destination in a way that refuses a concurrent reader
/// AND it still exposes the absent name, so it adds DRA-225's share-mask failure back on top
/// of the one it was supposed to remove. <b>Do not "restore" it.</b> The counter that named
/// <c>openRefused</c> separately is the only reason that second failure was visible at
/// all.</description></item>
/// </list>
///
/// <para><b>So is the primitive one layer down, on its own</b> —
/// <c>SetFileInformationByHandle(FileRenameInfo)</c> with <c>ReplaceIfExists</c>, which is
/// usually described as the atomic rename and is not exposed by <c>System.IO</c> at all. Much
/// better and still not enough: red <b>1 run in 8</b>, <c>absentName=379</c> of 67,034. Over a
/// name another process is OPENING, the kernel still unlinks the replaced entry and installs
/// the new one as two observable steps.</para>
///
/// <para><b>What closes it is the FLAG, not the call:</b> <c>FileRenameInfoEx</c> with
/// <c>FILE_RENAME_FLAG_POSIX_SEMANTICS</c>, which exists precisely to replace a file that is
/// open — the replaced file is unlinked and the name is repointed together, and an existing
/// handle keeps reading the file it already has. Measured over 20 mask runs: <b>absentName 0,
/// every run</b>, against 259–951 per run for <c>File.Move</c>. That is DRA-257's defect gone
/// rather than reduced, and it is the only one of the four that gets there.</para>
///
/// <para><b>It leaves a residual of a different mode, and that is stated rather than
/// rounded off:</b> 1–2 reads per ~33,000 come back <c>openDenied</c> — the replaced entry
/// caught mid-unlink, <c>ERROR_ACCESS_DENIED</c> rather than a name resolving to nothing. It is
/// 300× smaller than what it replaced and it is a transient by construction, which is what
/// finally earned <see cref="WholeFilePublish.Read"/> the bounded retry that was deleted from
/// it as unguardable in DRA-228 — see that method for why the retry covers THIS mode and
/// deliberately still does not cover an absent name.</para>
///
/// <para><b>It fails CLOSED, by throwing <see cref="IOException"/></b> — the same thing every
/// refused rename has always thrown — so <see cref="WholeFilePublish.Write"/>'s existing retry
/// and its skip-to-keep-the-previous-file-whole contract cover it unchanged, and nothing here
/// needs a policy of its own. A rename refused because a reader holds the destination without
/// <c>FILE_SHARE_DELETE</c> is exactly the transient this class's caller already retries.</para>
///
/// <para><b>Windows-only, stated rather than guarded.</b> The repo has been Windows-only since
/// 2026-09-04 (the Avalonia lane is on `legacy-v1`), and a fallback to <c>File.Move</c> on a
/// non-Windows runtime would be an untestable arm that silently restores the defect on the one
/// platform nobody runs — which is exactly how DRA-228's first fallback put the original bug
/// back at 71%. So there is no fallback and this throws whatever the OS throws.</para>
///
/// <para>This project is `net10.0` rather than `net10.0-windows` because Core and UI.Shared are
/// the UI-toolkit-free seam <c>ArchitectureTests</c> pins, and that is about WPF, not about
/// kernel32. A <c>[SupportedOSPlatform("windows")]</c> here is deliberately NOT applied: it is
/// correct, and CA1416 would then propagate up through <see cref="WholeFilePublish.Write"/> to
/// every `net10.0` caller including the test project, so the marker would be paid for in
/// warnings on the platform we actually ship. It is written down here instead.</para>
/// </summary>
internal static class AtomicRename
{
    /// <summary><c>FILE_INFO_BY_HANDLE_CLASS.FileRenameInfoEx</c> — Windows 10 1709 and
    /// later. The POSIX flag lives only on this class.</summary>
    private const int FileRenameInfoEx = 22;

    /// <summary><c>FILE_INFO_BY_HANDLE_CLASS.FileRenameInfo</c> — the older class, which takes
    /// the same structure with a BOOLEAN where the flags are. See <see cref="_useLegacyClass"/>
    /// for when it is used and why it is not simply the one we call.</summary>
    private const int FileRenameInfo = 3;

    private const int ReplaceIfExists = 0x00000001;
    private const int PosixSemantics = 0x00000002;

    /// <summary>
    /// <c>ERROR_INVALID_PARAMETER</c> — what <c>SetFileInformationByHandle</c> answers for an
    /// information class the running kernel does not know.
    /// </summary>
    private const int ErrorInvalidParameter = 87;

    /// <summary>
    /// Whether this kernel refused <see cref="FileRenameInfoEx"/>, latched after the first
    /// refusal so the probe is paid once rather than on every publish.
    ///
    /// <para><b>The fallback is here because the alternative failure is silent and
    /// expensive.</b> <c>FileRenameInfoEx</c> needs Windows 10 1709; this project pins no
    /// <c>SupportedOSPlatformVersion</c>, so an older box is reachable. Without a fallback the
    /// rename would fail, <see cref="WholeFilePublish.Write"/> would retry it 24 times —
    /// spinning and then SLEEPING, on the UI thread, every tick — and then drop the version,
    /// forever. The dump would simply stop advancing and nothing would say why. Losing the
    /// POSIX flag costs the 1-in-20 residual back; losing the publish costs the file.</para>
    ///
    /// <para><b>It is not the primary path</b> precisely because it is measurably worse:
    /// <c>FileRenameInfo</c> alone was red 1 run in 8 at <c>absentName=379</c> of 67,034 on the
    /// mask. It is the degraded answer for a kernel that cannot give the better one.</para>
    /// </summary>
    private static bool _useLegacyClass;

    private const uint Delete = 0x00010000;
    private const uint Synchronize = 0x00100000;
    private const uint ShareAll = 0x00000001 | 0x00000002 | 0x00000004;  // READ | WRITE | DELETE
    private const uint OpenExisting = 3;
    private const uint AttributeNormal = 0x00000080;

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true,
        CharSet = CharSet.Unicode, BestFitMapping = false)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle hFile, int fileInformationClass, IntPtr lpFileInformation, uint dwBufferSize);

    private static long _renames;

    /// <summary>
    /// How many renames have SUCCEEDED through <see cref="Over"/> in this process.
    ///
    /// <para><b>It exists because the honest mutant survived everything else.</b> Reverting
    /// <see cref="WholeFilePublish.Write"/>'s live-name arm to <c>File.Move(overwrite: true)</c>
    /// — the exact DRA-257 defect — left all ten tests green: the outcome enum still said
    /// <c>Replaced</c>, because a value a mutation can keep while changing the call underneath
    /// it is a label, not an observation (trap 78, and trap 64b — <c>Outcome.Replaced</c> was
    /// being read as a proxy for "the atomic path ran"). The only surviving guard was the
    /// 2-core race, and that one is GREEN on a 32-core box against the broken code (trap 77),
    /// so on every hosted runner with enough cores the revert would have merged clean.</para>
    ///
    /// <para>This is the fact rather than the proxy: the publish went through this code. It is
    /// not a diagnostic and nothing in <c>src/</c> reads it.</para>
    /// </summary>
    internal static long Renames => Interlocked.Read(ref _renames);

    /// <summary>
    /// Rename <paramref name="source"/> onto <paramref name="destination"/>, superseding it.
    /// Throws <see cref="IOException"/> if the rename is refused, so a caller retries it the
    /// same way it retries any other refused rename.
    /// </summary>
    internal static void Over(string source, string destination)
    {
        if (!_useLegacyClass)
        {
            if (Rename(source, destination, FileRenameInfoEx,
                    ReplaceIfExists | PosixSemantics, out var error))
                return;

            // Only ERROR_INVALID_PARAMETER means "this kernel does not know that class".
            // Anything else is an ordinary refusal and is reported as one — latching the
            // fallback on a sharing violation would quietly downgrade every later publish on
            // a box that was perfectly capable of the better call.
            if (error != ErrorInvalidParameter)
                throw Refused($"could not rename '{source}' over '{destination}'", error);

            _useLegacyClass = true;
        }

        RenameLegacy(source, destination);
    }

    /// <summary>The older information class, called directly. <b>It is internal so that
    /// <c>TheLegacyRenameClassStillPublishes</c> can exercise it for real</b> — the selection
    /// above cannot be reached on any box this repo runs on, and an arm that only a Windows
    /// build from 2016 can enter is a claim rather than code unless the CALL itself is
    /// tested (trap 78, the lesson this file's deleted reader retry already paid for).</summary>
    internal static void RenameLegacy(string source, string destination)
    {
        if (!Rename(source, destination, FileRenameInfo, ReplaceIfExists, out var error))
            throw Refused($"could not rename '{source}' over '{destination}'", error);
    }

    private static bool Rename(string source, string destination, int infoClass, int flags,
        out int error)
    {
        // DELETE is what a rename needs on the SOURCE handle — the operation removes its
        // directory entry — and the share mask is wide for the same reason WholeFilePublish's
        // reader is: refusing others is how DRA-225's 27% blanked dumps happened.
        using var handle = CreateFile(source, Delete | Synchronize, ShareAll, IntPtr.Zero,
            OpenExisting, AttributeNormal, IntPtr.Zero);

        if (handle.IsInvalid)
            throw Refused($"could not open '{source}' to rename it", Marshal.GetLastWin32Error());

        var target = Path.GetFullPath(destination);

        // FILE_RENAME_INFO: { ReplaceIfExists (padded to a pointer), RootDirectory (pointer),
        // FileNameLength (int), FileName[] }. FileNameLength is in BYTES and excludes the
        // terminator, but the buffer carries one — the kernel reads the length, a stale
        // terminator is what a debugger prints.
        var header = IntPtr.Size + IntPtr.Size + sizeof(int);
        var nameBytes = target.Length * sizeof(char);
        var size = header + nameBytes + sizeof(char);

        // Built in a managed array and PINNED rather than marshalled into unmanaged memory:
        // the array is zeroed by the runtime, so RootDirectory = NULL and the terminator are
        // facts rather than two more writes to get right.
        // The two classes take the SAME structure — FileRenameInfoEx reads the first field as
        // a flags DWORD where FileRenameInfo reads a BOOLEAN in its low byte, and
        // ReplaceIfExists is 1 in both readings. One builder, so the layout cannot drift
        // between the primary path and its fallback (trap 4).
        var buffer = new byte[size];
        BitConverter.TryWriteBytes(buffer.AsSpan(0), flags);
        BitConverter.TryWriteBytes(buffer.AsSpan(IntPtr.Size * 2), nameBytes);
        System.Text.Encoding.Unicode.GetBytes(target, 0, target.Length, buffer, header);

        var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            if (!SetFileInformationByHandle(handle, infoClass,
                    pinned.AddrOfPinnedObject(), (uint)size))
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            Interlocked.Increment(ref _renames);
            error = 0;
            return true;
        }
        finally
        {
            pinned.Free();
        }
    }

    /// <summary>A Win32 error as an <see cref="IOException"/> carrying its HRESULT, so a
    /// caller's <c>catch (IOException)</c> sees it and a reader of the message can still name
    /// the error rather than guessing at it (trap 75: one code for several causes is how the
    /// causes get guessed). The code is PASSED rather than re-read — <c>GetLastWin32Error</c>
    /// answers about the most recent call, and by the time a decision has been made about it
    /// that is no longer necessarily the call that failed.</summary>
    private static IOException Refused(string what, int error) =>
        new($"{what}: {new Win32Exception(error).Message} ({error})",
            unchecked((int)(0x80070000 | (uint)error)));
}
