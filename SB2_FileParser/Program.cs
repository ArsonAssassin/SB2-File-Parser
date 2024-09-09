using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using Newtonsoft.Json.Converters;

namespace DC2AP
{
    static class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SB2 File Parser");
            Console.WriteLine("Written by ArsonAssassin");
            var sb2Path = "";
            if (args.Length > 0)
            {
                sb2Path = args[0];
            }
            else
            {
                Console.WriteLine("Please input SB2 file path");
                sb2Path = Console.ReadLine();
            }
            SB2 fullFile = new SB2();
            using (var fileStream = new FileStream(sb2Path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fileStream))
            {
                //Parse Header
                fullFile.Header = new SB2Header();
                var magic = new string(reader.ReadChars(4));
                if (magic != "SB2\0")
                    throw new InvalidDataException("Invalid magic number in header.");
                fullFile.Header.Magic = magic;
                fullFile.Header.unk_function_data_offset = ReadUint(reader);
                fullFile.Header.codeStartByteOffset = ReadUint(reader);
                fullFile.Header.header_byte_count = ReadUint(reader);
                fullFile.Header.external_function_count = ReadUint(reader);
                fullFile.Header.unk0 = ReadUint(reader);
                fullFile.Header.global_variable_count = ReadUint(reader);

                fullFile.Header.unk1 = ReadUint(reader);
                fullFile.Header.unk2 = ReadUint(reader);
                fullFile.Header.unk3 = ReadUint(reader);
                fullFile.Header.unk4 = ReadUint(reader);
                fullFile.Header.unk5 = ReadUint(reader);
                fullFile.Header.unk6 = ReadUint(reader);
                fullFile.Header.unk7 = ReadUint(reader);
                fullFile.Header.unk8 = ReadUint(reader);
                fullFile.Header.unk9 = ReadUint(reader);

                Console.WriteLine("Header Parsed successfully");
                Console.WriteLine(JsonConvert.SerializeObject(fullFile.Header, Formatting.Indented));
                fullFile.ExternalFunctionTable = new List<SB2ExternalFunctionTableEntry>();
                for (int i = 0; i < fullFile.Header.external_function_count; i++)
                {
                    SB2ExternalFunctionTableEntry sB2ExternalFunctionTableEntry = new SB2ExternalFunctionTableEntry();
                    sB2ExternalFunctionTableEntry.Id = ReadUint(reader);
                    sB2ExternalFunctionTableEntry.function_data_byte_offset = ReadUint(reader);
                    fullFile.ExternalFunctionTable.Add(sB2ExternalFunctionTableEntry);
                }
                Console.WriteLine("External Function Table Parsed successfully");
                Console.WriteLine(JsonConvert.SerializeObject(fullFile.ExternalFunctionTable, Formatting.Indented));
                fullFile.Functions = new List<SB2Function>();
                for (int i = 0; i < fullFile.ExternalFunctionTable.Count; i++)
                {
                    var functionEntry = fullFile.ExternalFunctionTable[i];
                    SB2Function func = new SB2Function();
                    func.Instructions = new List<Sb2Instruction>();
                    reader.BaseStream.Seek(functionEntry.function_data_byte_offset, SeekOrigin.Begin);
                    func.CodeStartOffset = ReadUint(reader);
                    func.NameByteOffset = ReadUint(reader);
                    func.StackSize = ReadUint(reader);
                    func.ArgumentCount = ReadUint(reader);
                    func.unk0 = ReadUint(reader);
                    func.unk1 = ReadUint(reader);
                    func.unk2 = ReadUint(reader);
                    func.unk3 = ReadUint(reader);
                    func.unk4 = ReadUint(reader);
                    func.unk5 = ReadUint(reader);
                    func.unk6 = ReadUint(reader);
                    func.unk7 = ReadUint(reader);
                    func.unk8 = ReadUint(reader);
                    func.unk9 = ReadUint(reader);

                    reader.BaseStream.Seek(func.CodeStartOffset, SeekOrigin.Begin);
                    OpCode currentOpCode = OpCode.count;
                    while (!(currentOpCode == OpCode._end))
                    {
                        var instruction = ReadInstruction(reader);
                        func.Instructions.Add(instruction);
                        currentOpCode = instruction.OpCode;
                    }
                    if(fullFile.ExternalFunctionTable.Last().Id != functionEntry.Id)
                    {
                        var nextFuncStart = fullFile.ExternalFunctionTable[i + 1].function_data_byte_offset;
                        var currentLocation = reader.BaseStream.Position;
                        var currentDataSize = nextFuncStart - reader.BaseStream.Position;
                        byte[] localData = new byte[currentDataSize];
                        reader.BaseStream.Read(localData, 0, localData.Length);
                        func.LocalData = localData;
                    }
                    else
                    {
                        var currentLocation = reader.BaseStream.Position;
                        var currentDataSize = reader.BaseStream.Length - reader.BaseStream.Position;
                        byte[] localData = new byte[currentDataSize];
                        reader.BaseStream.Read(localData, 0, localData.Length);
                        func.LocalData = localData;
                    }

                    fullFile.Functions.Add(func);
                    Console.WriteLine("Function read successfully");
                    Console.WriteLine(JsonConvert.SerializeObject(func, Formatting.Indented));
                }
                Console.WriteLine("Function list extract complete");
            }

            Console.WriteLine("File Parsed Successfully.");
            var json = JsonConvert.SerializeObject(fullFile, Formatting.Indented);
            var jsonPath = sb2Path.Replace(".stb", ".json");
            File.WriteAllText(jsonPath, json);
        }
        
        private static uint ReadUint(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            return BitConverter.ToUInt32(bytes, 0);
        }
        private static Sb2Instruction ReadInstruction(BinaryReader reader)
        {
            var instruction = new Sb2Instruction();
            var instructionBits = ReadUint(reader);
            instruction.OpCode = (OpCode)(instructionBits & 0xFF);

            switch (instruction.OpCode)
            {
                case OpCode._push_stack:
                case OpCode._push_ptr:
                    instruction.Mode = (instructionBits >> 24) & 0xFF;
                    instruction.Address = (instructionBits >> 8) & 0xFFFF;
                    break;
                case OpCode._push:
                    instruction.Value = (instructionBits >> 12) & 0xFFF;
                    instruction.Type = (DataType)((instructionBits >> 8) & 0xF);
                    break;
                case OpCode._cmp:
                    instruction.Function = (ComparisonFunction)((instructionBits >> 8) & 0xF);
                    break;
                case OpCode._jmp:
                    instruction.Address = (instructionBits >> 8) & 0xFFFFFF;
                    break;
                case OpCode._bf:
                case OpCode._bt:
                    instruction.Address = (instructionBits >> 8) & 0xFFFFFF;
                    instruction.Restore = (instructionBits >> 24) & 0xFF;
                    break;
                case OpCode._div:
                case OpCode._mod:
                case OpCode._neg:
                case OpCode._itof:
                case OpCode._ftoi:
                case OpCode._deref:
                case OpCode._pop:
                case OpCode._add:
                case OpCode._sub:
                case OpCode._mul:
                    // These opcodes follow the Empty encoding
                    // No additional fields to extract
                    break;
                default:
                    break;
            }
            return instruction;
        }
    }

    public class SB2
    {
        public SB2Header Header { get; set; }
        public List<SB2ExternalFunctionTableEntry> ExternalFunctionTable { get; set; }
        public List<SB2Function> Functions { get; set; }


    }
    public class SB2Header()
    {
        public string Magic { get; set; }
        public uint unk_function_data_offset { get; set; }
        public uint codeStartByteOffset { get; set; }
        public uint header_byte_count { get; set; }
        public uint external_function_count { get; set; }
        public uint unk0 { get; set; }
        public uint global_variable_count { get; set; }
        public uint unk1 { get; set; }
        public uint unk2 { get; set; }
        public uint unk3 { get; set; }
        public uint unk4 { get; set; }
        public uint unk5 { get; set; }
        public uint unk6 { get; set; }
        public uint unk7 { get; set; }
        public uint unk8 { get; set; }
        public uint unk9 { get; set; }
    }
    public class SB2ExternalFunctionTableEntry
    {
        public uint Id { get; set; }
        public uint function_data_byte_offset { get; set; }
    }

    public class SB2Function()
    {
        public uint CodeStartOffset { get; set; }
        public uint NameByteOffset { get; set; }
        public uint StackSize { get; set; }
        public uint ArgumentCount { get; set; }
        public uint unk0 { get; set; }
        public uint unk1 { get; set; }
        public uint unk2 { get; set; }
        public uint unk3 { get; set; }
        public uint unk4 { get; set; }
        public uint unk5 { get; set; }
        public uint unk6 { get; set; }
        public uint unk7 { get; set; }
        public uint unk8 { get; set; }
        public uint unk9 { get; set; }
        public List<Sb2Instruction> Instructions { get; set; }
        public byte[] LocalData { get; set; }
    }
    public class Sb2Instruction
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public OpCode OpCode { get; set; }
        public uint Mode { get; set; }
        public uint Address { get; set; }
        public uint Value { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public DataType Type { get; set; }
        public uint Restore { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public ComparisonFunction Function { get; set; }
    }
    public enum ComparisonFunction
    {
        _eq = 0,
        _ne = 1,
        _lt = 2,
        _le = 3,
        _gt = 4,
        _ge = 5,
        count
    }
    public enum DataType
    {
        invalid,
        _int = 1,
        _flt = 2,
        _str = 3,
        _ptr = 4,
        count
    }
    public enum OpCode
    {
        _end = 0,
        _push_stack = 1,
        _push_ptr = 2,
        _push = 3,
        _pop = 4,
        _deref = 5,
        _add = 6,
        _sub = 7,
        _mul = 8,
        _div = 9,
        _mod = 10,
        _neg = 11,
        _itof = 12,
        _ftoi = 13,
        _cmp = 14,
        _ret = 15,
        _jmp = 16,
        _bf = 17,
        _bt = 18,
        _call = 19,
        _print = 20,
        _ext = 21,
        _nop = 22,
        _yld = 23,
        _and = 24,
        _or = 25,
        _not = 26,
        _exit = 27,
        _unk1 = 28,
        _sin = 29,
        _cos = 30,
        count
    }
}