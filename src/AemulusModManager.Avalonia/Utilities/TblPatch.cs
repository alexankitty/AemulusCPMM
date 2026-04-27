using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AemulusModManager.Utilities;
using AemulusModManager.Utilities.TblPatching;
using Newtonsoft.Json;

namespace AemulusModManager;

/// <summary>
/// Cross-platform port of TblPatch. Handles .tblpatch and .tbp (JSON) patching
/// for game TBL files, using PAKPackHelper for archive operations.
/// </summary>
public static class tblPatch {
    private static string AppDir => AemulusModManager.Avalonia.Utilities.AppPaths.ExeDir;
    private static string DataDir => AemulusModManager.Avalonia.Utilities.AppPaths.DataDir;
    private static string? tblDir;

    private static byte[] SliceArray(byte[] source, int start, int end) {
        int length = end - start;
        byte[] dest = new byte[length];
        Array.Copy(source, start, dest, 0, length);
        return dest;
    }

    private static void unpackTbls(string archive, string game) {
        if (game == "Persona 3 FES" || game == "Persona 5 Royal (Switch)")
            return;
        PAKPackHelper.PAKPackCMD($"unpack \"{archive}\" \"{tblDir}\"");
    }

    private static void repackTbls(string tbl, string archive, string game) {
        string? parent = null;
        if (game == "Persona 4 Golden" || game == "Persona 4 Golden (Vita)" || game == "Persona 3 Portable") {
            parent = "battle";
            if (Path.GetFileName(tbl).Equals("ITEMTBL.TBL")) {
                parent = "init";
                tbl = tbl.Replace(Path.Combine("battle", "ITEMTBL.TBL"), Path.Combine("init", "itemtbl.bin"));
            }
        }
        else if (game == "Persona 5" || game == "Persona 5 Royal (PS4)")
            parent = "table";
        else if (game == "Persona 3 FES")
            return;
        PAKPackHelper.PAKPackCMD($"replace \"{archive}\" {parent}/{Path.GetFileName(tbl)} \"{tbl}\"");
    }

    private static readonly string[] p4gTables = { "SKILL", "UNIT", "MSG", "PERSONA", "ENCOUNT", "EFFECT", "MODEL", "AICALC", "ITEMTBL" };
    private static readonly string[] p3pTables = { "SKILL", "UNIT", "MSG", "PERSONA", "ENCOUNT", "EFFECT", "MODEL", "AICALC" };
    private static readonly string[] p3fTables = { "SKILL", "SKILL_F", "UNIT", "UNIT_F", "MSG", "PERSONA", "PERSONA_F", "ENCOUNT", "ENCOUNT_F", "EFFECT", "MODEL", "AICALC", "AICALC_F" };
    private static readonly string[] p5Tables = { "AICALC", "ELSAI", "ENCOUNT", "EXIST", "ITEM", "NAME", "PERSONA", "PLAYER", "SKILL", "TALKINFO", "UNIT", "VISUAL" };
    private static readonly string[] pqNameTbls = { "battle/table/personanametable.tbl", "battle/table/enemynametable.tbl", "battle/table/skillnametable.tbl" };

    public static void Patch(List<string> ModList, string modDir, bool useCpk, string cpkLang, string game) {
        if (!PAKPackHelper.IsAvailable()) {
            ParallelLogger.Log("[ERROR] PAKPack not available. TBL patching requires PAKPack + mono on Linux.");
            return;
        }
        ParallelLogger.Log("[INFO] Patching TBLs...");

        // Determine archive path
        string? archive = null;
        if (game == "Persona 4 Golden") {
            if (useCpk)
                archive = Path.Combine(Path.GetFileNameWithoutExtension(cpkLang), "init_free.bin");
            else {
                archive = cpkLang switch {
                    "data_e.cpk" => Path.Combine("data00004", "init_free.bin"),
                    "data.cpk" => Path.Combine("data00001", "init_free.bin"),
                    "data_c.cpk" => Path.Combine("data00006", "init_free.bin"),
                    "data_k.cpk" => Path.Combine("data00005", "init_free.bin"),
                    _ => Path.Combine("data00004", "init_free.bin"),
                };
            }
        }
        else if (game == "Persona 4 Golden (Vita)")
            archive = "init_free.bin";
        else if (game == "Persona 3 Portable")
            archive = Path.Combine("data", "init_free.bin");
        else if (game == "Persona 5" || game == "Persona 5 Royal (PS4)")
            archive = Path.Combine("battle", "table.pac");

        if (game != "Persona 3 FES" && game != "Persona 5 Royal (Switch)" && game != "Persona Q" && game != "Persona Q2") {
            var archiveFull = Path.Combine(modDir, archive!);
            if (!File.Exists(archiveFull)) {
                var origArchive = Path.Combine(DataDir, "Original", game, archive!);
                if (File.Exists(origArchive)) {
                    Directory.CreateDirectory(Path.GetDirectoryName(archiveFull)!);
                    File.Copy(origArchive, archiveFull, true);
                    ParallelLogger.Log($"[INFO] Copied over {archive} from Original directory.");
                }
                else {
                    ParallelLogger.Log($"[WARNING] {archive} not found in output directory or Original directory.");
                    return;
                }
            }

            tblDir = Path.Combine(modDir, Path.ChangeExtension(archive!, null) + "_tbls");
            ParallelLogger.Log($"[INFO] Unpacking TBLs from {archive}...");
            unpackTbls(archiveFull, game);
        }

        var editedTables = new List<string>();
        List<NameSection>? sections = null;

        foreach (string dir in ModList) {
            ParallelLogger.Log($"[INFO] Searching for/applying tblpatches in {dir}...");
            var tblpatchesDir = Path.Combine(dir, "tblpatches");
            if (!Directory.Exists(tblpatchesDir)) {
                ParallelLogger.Log($"[INFO] No tblpatches folder found in {dir}");
                continue;
            }

            // Apply .tblpatch files
            foreach (var t in Directory.GetFiles(tblpatchesDir, "*.tblpatch", SearchOption.AllDirectories)) {
                byte[] file = File.ReadAllBytes(t);
                string fileName = Path.GetFileName(t);
                ParallelLogger.Log($"[INFO] Loading {fileName}");
                if (file.Length < 3) {
                    ParallelLogger.Log("[ERROR] Improper .tblpatch format.");
                    continue;
                }

                string tblCode = Encoding.ASCII.GetString(SliceArray(file, 0, 3));
                string? tblName = ResolveTblName(tblCode, game);
                if (tblName == null) continue;

                if (!editedTables.Contains(tblName))
                    editedTables.Add(tblName);

                if (tblName != "NAME.TBL") {
                    if (file.Length < 12) {
                        ParallelLogger.Log("[ERROR] Improper .tblpatch format.");
                        continue;
                    }
                    byte[] byteOffset = SliceArray(file, 3, 11);
                    Array.Reverse(byteOffset, 0, 8);
                    long offset = BitConverter.ToInt64(byteOffset, 0);
                    byte[] fileContents = SliceArray(file, 11, file.Length);

                    string? tblPath = GetTblPath(tblName, modDir, game);
                    if (tblPath == null) continue;

                    byte[] tblBytes = File.ReadAllBytes(tblPath);
                    fileContents.CopyTo(tblBytes, offset);
                    File.WriteAllBytes(tblPath, tblBytes);
                }
                else {
                    var nameTblPath = Path.Combine(tblDir!, "table", tblName);
                    sections = GetNameSections(nameTblPath);
                    if (file.Length < 6) {
                        ParallelLogger.Log("[ERROR] Improper .tblpatch format.");
                        continue;
                    }
                    var temp = ReplaceName(sections, file, null, game);
                    if (temp != null) {
                        sections = temp;
                        WriteNameTbl(sections, nameTblPath);
                    }
                }
            }

            // Apply .tbp (JSON) patches
            var tables = new List<Table>();
            foreach (var t in Directory.GetFiles(tblpatchesDir, "*.tbp", SearchOption.AllDirectories)) {
                TablePatches? tablePatches = null;
                try {
                    tablePatches = JsonConvert.DeserializeObject<TablePatches>(File.ReadAllText(t));
                }
                catch (Exception ex) {
                    ParallelLogger.Log($"[ERROR] Couldn't deserialize {t} ({ex.Message}), skipping...");
                    continue;
                }
                if (tablePatches?.Version != 1) {
                    ParallelLogger.Log($"[ERROR] Invalid version for {t}, skipping...");
                    continue;
                }
                if (tablePatches.Patches != null) {
                    foreach (var patch in tablePatches.Patches) {
                        ParallelLogger.Log($"[INFO] Current patch: tbl={patch.tbl}, section={patch.section}, offset={patch.offset}, index={patch.index}");

                        if (!tables.Exists(x => x.tableName == patch.tbl)) {
                            if (!IsTableValid(patch.tbl, game)) {
                                ParallelLogger.Log($"[ERROR] {patch.tbl} doesn't exist in {game}, skipping...");
                                continue;
                            }

                            var table = new Table();
                            string? tablePath = GetTablePathForTbp(patch.tbl, modDir, game, cpkLang);
                            if (tablePath == null) continue;

                            if (patch.tbl == "NAME")
                                table.nameSections = GetNameSections(tablePath);
                            else if (pqNameTbls.Contains(patch.tbl))
                                table.nameSections = GetNameSectionQ(tablePath);
                            else
                                table.sections = GetSections(tablePath, game);
                            table.tableName = patch.tbl;
                            tables.Add(table);
                        }

                        if (patch.tbl == "NAME" || pqNameTbls.Contains(patch.tbl))
                            tables.Find(x => x.tableName == patch.tbl)!.nameSections = ReplaceName(tables.Find(x => x.tableName == patch.tbl)!.nameSections!, null, patch, game);
                        else
                            tables.Find(x => x.tableName == patch.tbl)!.sections = ReplaceSection(tables.Find(x => x.tableName == patch.tbl)!.sections!, patch);
                    }
                }
            }

            // Write modified tables
            foreach (var table in tables) {
                if (!editedTables.Contains($"{table.tableName}.TBL"))
                    editedTables.Add($"{table.tableName}.TBL");
                else if ((game == "Persona Q" || game == "Persona Q2") && !editedTables.Contains(table.tableName))
                    editedTables.Add(table.tableName);

                string? path = GetTableWritePath(table.tableName, modDir, game, cpkLang);
                if (path == null) continue;

                if (table.tableName == "NAME")
                    WriteNameTbl(table.nameSections!, path);
                else if (pqNameTbls.Contains(table.tableName))
                    WriteNameTblQ(table.nameSections!, path);
                else
                    WriteTbl(table.sections!, path, game);
            }

            ParallelLogger.Log($"[INFO] Applied patches from {dir}");
        }

        if (game != "Persona 3 FES" && game != "Persona 5 Royal (Switch)" && game != "Persona Q" && game != "Persona Q2") {
            foreach (string u in editedTables) {
                if (u == "ITEMTBL.TBL")
                    ParallelLogger.Log($"[INFO] Replacing itemtbl.bin in {archive}");
                else
                    ParallelLogger.Log($"[INFO] Replacing {u} in {archive}");
                if (game == "Persona 5" || game == "Persona 5 Royal (PS4)")
                    repackTbls(Path.Combine(tblDir!, "table", u), Path.Combine(modDir, archive!), game);
                else
                    repackTbls(Path.Combine(tblDir!, "battle", u), Path.Combine(modDir, archive!), game);
            }

            ParallelLogger.Log("[INFO] Deleting temp tbl folder...");
            if (tblDir != null && Directory.Exists(tblDir))
                Directory.Delete(tblDir, true);
        }
        ParallelLogger.Log("[INFO] Finished patching TBLs!");
    }

    private static string? ResolveTblName(string code, string game) {
        bool isP5 = game == "Persona 5" || game == "Persona 5 Royal (PS4)" || game == "Persona 5 Royal (Switch)";
        bool isP3F = game == "Persona 3 FES";

        switch (code) {
            case "SKL": return "SKILL.TBL";
            case "UNT": return "UNIT.TBL";
            case "MSG":
                if (isP5) { ParallelLogger.Log($"[WARNING] MSG.TBL not found in {game}, skipping"); return null; }
                return "MSG.TBL";
            case "PSA": return "PERSONA.TBL";
            case "ENC": return "ENCOUNT.TBL";
            case "EFF":
                if (isP5) { ParallelLogger.Log($"[WARNING] EFFECT.TBL not found in {game}, skipping"); return null; }
                return "EFFECT.TBL";
            case "MDL":
                if (isP5) { ParallelLogger.Log($"[WARNING] MODEL.TBL not found in {game}, skipping"); return null; }
                return "MODEL.TBL";
            case "AIC": return "AICALC.TBL";
            case "AIF":
                if (!isP3F) { ParallelLogger.Log($"[WARNING] AICALC_F.TBL not found in {game}, skipping"); return null; }
                return "AICALC_F.TBL";
            case "ENF":
                if (!isP3F) { ParallelLogger.Log($"[WARNING] ENCOUNT_F.TBL not found in {game}, skipping"); return null; }
                return "ENCOUNT_F.TBL";
            case "PSF":
                if (!isP3F) { ParallelLogger.Log($"[WARNING] PERSONA_F.TBL not found in {game}, skipping"); return null; }
                return "PERSONA_F.TBL";
            case "SKF":
                if (!isP3F) { ParallelLogger.Log($"[WARNING] SKILL_F.TBL not found in {game}, skipping"); return null; }
                return "SKILL_F.TBL";
            case "UNF":
                if (!isP3F) { ParallelLogger.Log($"[WARNING] UNIT_F.TBL not found in {game}, skipping"); return null; }
                return "UNIT_F.TBL";
            case "EAI":
                if (!isP5) { ParallelLogger.Log($"[WARNING] ELSAI.TBL not found in {game}, skipping"); return null; }
                return "ELSAI.TBL";
            case "EXT":
                if (!isP5) { ParallelLogger.Log($"[WARNING] EXIST.TBL not found in {game}, skipping"); return null; }
                return "EXIST.TBL";
            case "ITM":
                if (!isP5) { ParallelLogger.Log($"[WARNING] ITEM.TBL not found in {game}, skipping"); return null; }
                return "ITEM.TBL";
            case "NME":
                if (!isP5) { ParallelLogger.Log($"[WARNING] NAME.TBL not found in {game}, skipping"); return null; }
                return "NAME.TBL";
            case "PLY":
                if (!isP5) { ParallelLogger.Log($"[WARNING] PLAYER.TBL not found in {game}, skipping"); return null; }
                return "PLAYER.TBL";
            case "TKI":
                if (!isP5) { ParallelLogger.Log($"[WARNING] TALKINFO.TBL not found in {game}, skipping"); return null; }
                return "TALKINFO.TBL";
            case "VSL":
                if (!isP5) { ParallelLogger.Log($"[WARNING] VISUAL.TBL not found in {game}, skipping"); return null; }
                return "VISUAL.TBL";
            default:
                ParallelLogger.Log($"[ERROR] Unknown tbl name code: {code}.");
                return null;
        }
    }

    private static string? GetTblPath(string tblName, string modDir, string game) {
        if (game == "Persona 3 FES") {
            var p = Path.Combine(modDir, "BTL", "BATTLE", tblName);
            if (!File.Exists(p)) {
                var orig = Path.Combine(DataDir, "Original", game, "BTL", "BATTLE", tblName);
                if (File.Exists(orig)) {
                    Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                    File.Copy(orig, p, true);
                    ParallelLogger.Log($"[INFO] Copied over {tblName} from Original directory.");
                }
                else {
                    ParallelLogger.Log($"[WARNING] {tblName} not found in output directory or Original directory.");
                    return null;
                }
            }
            return p;
        }
        else if (game == "Persona 5 Royal (Switch)") {
            var p = Path.Combine(modDir, "BASE", "BATTLE", "TABLE", tblName);
            if (!File.Exists(p)) {
                var orig = Path.Combine(DataDir, "Original", game, "BASE", "BATTLE", "TABLE", tblName);
                if (File.Exists(orig)) {
                    Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                    File.Copy(orig, p, true);
                    ParallelLogger.Log($"[INFO] Copied over {tblName} from Original directory.");
                }
                else {
                    ParallelLogger.Log($"[WARNING] {tblName} not found in output directory or Original directory.");
                    return null;
                }
            }
            return p;
        }
        else {
            if (game == "Persona 4 Golden" || game == "Persona 4 Golden (Vita)")
                return Path.Combine(tblDir!, "battle", tblName);
            else
                return Path.Combine(tblDir!, "table", tblName);
        }
    }

    private static bool IsTableValid(string tblName, string game) {
        if ((game == "Persona 4 Golden" || game == "Persona 4 Golden (Vita)") && !p4gTables.Contains(tblName)) return false;
        if (game == "Persona 3 FES" && !p3fTables.Contains(tblName)) return false;
        if ((game == "Persona 5" || game == "Persona 5 Royal (PS4)" || game == "Persona 5 Royal (Switch)") && !p5Tables.Contains(tblName)) return false;
        if (game == "Persona 3 Portable" && !p3pTables.Contains(tblName)) return false;
        if ((game == "Persona Q" || game == "Persona Q2") && !QTblExists(game, tblName)) return false;
        return true;
    }

    private static string? GetTablePathForTbp(string tblName, string modDir, string game, string cpkLang) {
        if (game == "Persona 3 FES") {
            var p = Path.Combine(modDir, "BTL", "BATTLE", $"{tblName}.TBL");
            if (!File.Exists(p)) {
                var orig = Path.Combine(DataDir, "Original", game, "BTL", "BATTLE", $"{tblName}.TBL");
                if (File.Exists(orig)) {
                    Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                    File.Copy(orig, p, true);
                    ParallelLogger.Log($"[INFO] Copied over {tblName}.TBL from Original directory.");
                }
                else {
                    ParallelLogger.Log($"[WARNING] {tblName}.TBL not found in output directory or Original directory.");
                    return null;
                }
            }
            return p;
        }
        else if (game == "Persona 5 Royal (Switch)") {
            var p = tblName.Equals("NAME", StringComparison.InvariantCultureIgnoreCase)
                ? Path.Combine(modDir, cpkLang, "BATTLE", "TABLE", $"{tblName}.TBL")
                : Path.Combine(modDir, "BASE", "BATTLE", "TABLE", $"{tblName}.TBL");
            var origP = tblName.Equals("NAME", StringComparison.InvariantCultureIgnoreCase)
                ? Path.Combine(DataDir, "Original", game, cpkLang, "BATTLE", "TABLE", $"{tblName}.TBL")
                : Path.Combine(DataDir, "Original", game, "BASE", "BATTLE", "TABLE", $"{tblName}.TBL");
            if (!File.Exists(p)) {
                if (File.Exists(origP)) {
                    Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                    File.Copy(origP, p, true);
                    ParallelLogger.Log($"[INFO] Copied over {tblName}.TBL from Original directory.");
                }
                else {
                    ParallelLogger.Log($"[WARNING] {tblName}.TBL not found in output directory or Original directory.");
                    return null;
                }
            }
            return p;
        }
        else if (game == "Persona Q" || game == "Persona Q2") {
            var p = Path.Combine(modDir, tblName);
            var origP = Path.Combine(DataDir, "Original", game, tblName);
            if (!File.Exists(p)) {
                if (File.Exists(origP)) {
                    Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                    File.Copy(origP, p, true);
                    ParallelLogger.Log($"[INFO] Copied over {tblName} from Original directory.");
                }
                else {
                    ParallelLogger.Log($"[WARNING] {tblName} not found in output directory or Original directory.");
                    return null;
                }
            }
            return p;
        }
        else if (game == "Persona 4 Golden" || game == "Persona 4 Golden (Vita)" || game == "Persona 3 Portable")
            return tblName.Equals("ITEMTBL") ? Path.Combine(tblDir!, "init", "itemtbl.bin") : Path.Combine(tblDir!, "battle", $"{tblName}.TBL");
        else if (game == "Persona 5" || game == "Persona 5 Royal (PS4)")
            return Path.Combine(tblDir!, "table", $"{tblName}.TBL");
        return null;
    }

    private static string? GetTableWritePath(string tblName, string modDir, string game, string cpkLang) {
        if (game == "Persona 3 FES")
            return Path.Combine(modDir, "BTL", "BATTLE", $"{tblName}.TBL");
        else if (game == "Persona 5 Royal (Switch)")
            return tblName.Equals("NAME", StringComparison.InvariantCultureIgnoreCase)
                ? Path.Combine(modDir, cpkLang, "BATTLE", "TABLE", $"{tblName}.TBL")
                : Path.Combine(modDir, "BASE", "BATTLE", "TABLE", $"{tblName}.TBL");
        else if (game == "Persona 4 Golden" || game == "Persona 4 Golden (Vita)" || game == "Persona 3 Portable")
            return tblName.Equals("ITEMTBL") ? Path.Combine(tblDir!, "init", "itemtbl.bin") : Path.Combine(tblDir!, "battle", $"{tblName}.TBL");
        else if (game == "Persona 5" || game == "Persona 5 Royal (PS4)")
            return Path.Combine(tblDir!, "table", $"{tblName}.TBL");
        else if (game == "Persona Q" || game == "Persona Q2")
            return Path.Combine(modDir, tblName);
        return null;
    }

    private static List<Section> GetSections(string tbl, string game) {
        var sections = new List<Section>();
        if (Path.GetFileName(tbl) == "itemtbl.bin") {
            var file = File.ReadAllBytes(tbl);
            sections.Add(new Section { size = file.Length, data = file });
            return sections;
        }
        bool bigEndian = game == "Persona 5" || game == "Persona 5 Royal (PS4)" || game == "Persona 5 Royal (Switch)";
        using var fs = new FileStream(tbl, FileMode.Open);
        using var br = new BinaryReader(fs);
        while (br.BaseStream.Position < fs.Length) {
            var section = new Section();
            if (bigEndian) {
                var data = br.ReadBytes(4);
                Array.Reverse(data);
                section.size = BitConverter.ToInt32(data, 0);
            }
            else if (game == "Persona Q" || game == "Persona Q2")
                section.size = (int)fs.Length;
            else
                section.size = br.ReadInt32();
            section.data = br.ReadBytes(section.size);
            if ((br.BaseStream.Position % 16) != 0)
                br.BaseStream.Position += 16 - (br.BaseStream.Position % 16);
            sections.Add(section);
        }
        return sections;
    }

    private static List<NameSection> GetNameSections(string tbl) {
        var sections = new List<NameSection>();
        byte[] tblBytes = File.ReadAllBytes(tbl);
        int pos = 0;
        while (pos < tblBytes.Length) {
            var section = new NameSection();
            section.pointersSize = BitConverter.ToInt32(SliceArray(tblBytes, pos, pos + 4).AsEnumerable().Reverse().ToArray(), 0);
            byte[] segment = SliceArray(tblBytes, pos + 4, pos + 4 + section.pointersSize);
            section.pointers = new List<UInt16>();
            for (int j = 0; j < segment.Length; j += 2)
                section.pointers.Add(BitConverter.ToUInt16(SliceArray(segment, j, j + 2).AsEnumerable().Reverse().ToArray(), 0));

            pos += section.pointersSize + 4;
            if ((pos % 16) != 0)
                pos += 16 - (pos % 16);

            section.namesSize = BitConverter.ToInt32(SliceArray(tblBytes, pos, pos + 4).AsEnumerable().Reverse().ToArray(), 0);
            segment = SliceArray(tblBytes, pos + 4, pos + 4 + section.namesSize);
            section.names = new List<byte[]>();
            var name = new List<byte>();
            foreach (var b in segment) {
                if (b == 0) {
                    section.names.Add(name.ToArray());
                    name = new List<byte>();
                }
                else
                    name.Add(b);
            }

            pos += section.namesSize + 4;
            if ((pos % 16) != 0)
                pos += 16 - (pos % 16);
            sections.Add(section);
        }
        return sections;
    }

    private static List<NameSection> GetNameSectionQ(string tbl) {
        var sections = new List<NameSection>();
        byte[] tblBytes = File.ReadAllBytes(tbl);
        int pos = 0;
        var section = new NameSection();
        section.pointersSize = BitConverter.ToInt16(SliceArray(tblBytes, pos, pos + 2), 0);
        byte[] segment = SliceArray(tblBytes, pos + 2, pos + 2 + section.pointersSize * 2);
        section.pointers = new List<ushort>();
        for (int i = 0; i < segment.Length; i += 2)
            section.pointers.Add(BitConverter.ToUInt16(SliceArray(segment, i, i + 2), 0));

        pos += section.pointersSize * 2 + 2;
        section.namesSize = tblBytes.Length - pos;
        segment = SliceArray(tblBytes, pos, pos + section.namesSize);
        section.names = new List<byte[]>();
        var name = new List<byte>();
        foreach (var b in segment) {
            if (b == 0) {
                section.names.Add(name.ToArray());
                name = new List<byte>();
            }
            else
                name.Add(b);
        }
        section.names.Add(Encoding.ASCII.GetBytes("owo what's this")); // dummy name for ptr past eof

        sections.Add(section);
        return sections;
    }

    private static List<NameSection>? ReplaceName(List<NameSection> sections, byte[]? patch, TablePatch? namePatch, string game) {
        int section = 0;
        int index = 0;
        byte[]? fileContents = null;

        if (patch != null) {
            section = Convert.ToInt32(patch[3]);
            index = BitConverter.ToInt16(SliceArray(patch, 4, 6).AsEnumerable().Reverse().ToArray(), 0);
            fileContents = SliceArray(patch, 6, patch.Length);
        }
        else if (namePatch != null) {
            if (namePatch.section == null || namePatch.index == null || namePatch.name == null) {
                ParallelLogger.Log("[ERROR] Incomplete patch, skipping...");
                return sections;
            }
            section = (int)namePatch.section;
            index = (int)namePatch.index;
            fileContents = ConvertName(namePatch.name);
            if (fileContents == null) return sections;
        }
        else {
            ParallelLogger.Log("[ERROR] No patch passed to replace function, skipping...");
            return sections;
        }

        if (section >= sections.Count) {
            ParallelLogger.Log("[ERROR] Section chosen is out of bounds, skipping...");
            return sections;
        }
        if (index < 0) {
            ParallelLogger.Log("[ERROR] Index cannot be negative, skipping...");
            return sections;
        }

        if (index >= sections[section].names.Count) {
            byte[] dummy = Encoding.ASCII.GetBytes("RESERVE");
            while (sections[section].names.Count < index) {
                sections[section].pointers.Add((ushort)(sections[section].pointers.Last() + sections[section].names.Last().Length + 1));
                sections[section].names.Add(dummy);
                sections[section].pointersSize += 2;
                sections[section].namesSize += dummy.Length + 1;
            }
            sections[section].pointers.Add((ushort)(sections[section].pointers.Last() + sections[section].names.Last().Length + 1));
            sections[section].names.Add(fileContents);
            sections[section].pointersSize += 2;
            sections[section].namesSize += fileContents.Length + 1;
        }
        else {
            int delta = fileContents.Length - sections[section].names[index].Length;
            sections[section].names[index] = fileContents;
            sections[section].namesSize += delta;
            for (int i = (game == "Persona Q" || game == "Persona Q2" ? index : index + 1); i < sections[section].pointers.Count; i++)
                sections[section].pointers[i] += (UInt16)delta;
        }
        return sections;
    }

    private static byte[]? ConvertName(string name) {
        string[] stringData = Regex.Split(name, @"(\[.*?\])");
        var byteName = new List<byte>();
        foreach (var part in stringData) {
            if (!part.Contains('[')) {
                foreach (byte b in Encoding.ASCII.GetBytes(part))
                    byteName.Add(b);
            }
            else {
                foreach (var hex in part.Substring(1, part.Length - 2).Split(' ')) {
                    if (hex.Length != 2) {
                        ParallelLogger.Log("[ERROR] Couldn't parse hex string, skipping...");
                        return null;
                    }
                    try {
                        byteName.Add(Convert.ToByte(hex, 16));
                    }
                    catch (Exception ex) {
                        ParallelLogger.Log($"[ERROR] Couldn't parse hex string ({ex.Message}), skipping...");
                        return null;
                    }
                }
            }
        }
        return byteName.ToArray();
    }

    private static List<Section> ReplaceSection(List<Section> sections, TablePatch patch) {
        if (patch.offset == null || patch.section == null || patch.data == null) {
            ParallelLogger.Log("[ERROR] Incomplete patch, skipping...");
            return sections;
        }
        int section = (int)patch.section;
        int offset = (int)patch.offset;
        string[] stringData = patch.data.Split(' ');
        byte[] data = new byte[stringData.Length];
        for (int i = 0; i < data.Length; i++) {
            try {
                data[i] = Convert.ToByte(stringData[i], 16);
            }
            catch (Exception ex) {
                ParallelLogger.Log($"[ERROR] Couldn't parse hex string {stringData[i]} ({ex.Message}), skipping...");
                return sections;
            }
        }
        if (offset < 0) {
            ParallelLogger.Log("[ERROR] Offset cannot be negative, skipping...");
            return sections;
        }
        if (section >= sections.Count) {
            ParallelLogger.Log("[ERROR] Section chosen is out of bounds, skipping...");
            return sections;
        }
        if (offset + data.Length >= sections[section].data.Length) {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(sections[section].data);
            while (offset >= ms.Length) bw.Write((byte)0);
            bw.BaseStream.Position = offset;
            bw.Write(data);
            sections[section].data = ms.ToArray();
            sections[section].size = sections[section].data.Length;
        }
        else {
            using var ms = new MemoryStream(sections[section].data);
            using var bw = new BinaryWriter(ms);
            if (offset >= ms.Length) {
                bw.BaseStream.Position = ms.Length - 1;
                while (offset >= ms.Length) bw.Write((byte)0);
            }
            bw.BaseStream.Position = offset;
            bw.Write(data);
        }
        return sections;
    }

    private static void WriteNameTbl(List<NameSection> sections, string path) {
        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        foreach (var section in sections) {
            bw.Write(BitConverter.GetBytes(section.pointersSize).AsEnumerable().Reverse().ToArray());
            foreach (var pointer in section.pointers)
                bw.Write(BitConverter.GetBytes(pointer).AsEnumerable().Reverse().ToArray());
            while (bw.BaseStream.Position % 16 != 0) bw.Write((byte)0);
            bw.Write(BitConverter.GetBytes(section.namesSize).AsEnumerable().Reverse().ToArray());
            foreach (var name in section.names) {
                bw.Write(name);
                bw.Write((byte)0);
            }
            while (bw.BaseStream.Position % 16 != 0) bw.Write((byte)0);
        }
    }

    private static void WriteNameTblQ(List<NameSection> sections, string path) {
        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        bw.Write(BitConverter.GetBytes((short)sections[0].pointersSize));
        foreach (var pointer in sections[0].pointers) bw.Write(BitConverter.GetBytes(pointer));
        foreach (var name in sections[0].names) {
            bw.Write(name);
            bw.Write((byte)0);
        }
    }

    private static void WriteTbl(List<Section> sections, string path, string game) {
        bool bigEndian = game == "Persona 5" || game == "Persona 5 Royal (PS4)" || game == "Persona 5 Royal (Switch)";
        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        if (((game == "Persona 4 Golden" || game == "Persona 4 Golden (Vita)") && Path.GetFileName(path).Equals("itemtbl.bin", StringComparison.InvariantCultureIgnoreCase))
            || game == "Persona Q" || game == "Persona Q2")
            bw.Write(sections[0].data);
        else {
            foreach (var section in sections) {
                if (bigEndian)
                    bw.Write(BitConverter.GetBytes(section.size).AsEnumerable().Reverse().ToArray());
                else
                    bw.Write(BitConverter.GetBytes(section.size));
                bw.Write(section.data);
                while (bw.BaseStream.Position % 16 != 0) bw.Write((byte)0);
            }
        }
    }

    private static bool QTblExists(string game, string tblPath) {
        if (game != "Persona Q" && game != "Persona Q2") return false;
        var csv = Path.Combine(AppDir, "Dependencies", "FilteredCpkCsv", $"filtered_data_pq{(game == "Persona Q2" ? "2" : "")}.csv");
        if (!File.Exists(csv)) {
            ParallelLogger.Log("[ERROR] Couldn't find CSV file in Dependencies/FilteredCpkCsv");
            return false;
        }
        var qTbls = File.ReadAllLines(csv).Where(t => Path.GetExtension(t) == ".tbl").ToList();
        return qTbls.Contains(tblPath);
    }
}

// Helper class for TBP deserialization
internal class Table {
    public string tableName { get; set; } = "";
    public List<Section>? sections { get; set; }
    public List<NameSection>? nameSections { get; set; }
}
