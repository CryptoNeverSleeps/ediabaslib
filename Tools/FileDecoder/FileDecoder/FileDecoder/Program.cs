﻿using System;
using System.IO;

namespace FileDecoder
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("No input file specified");
                return 1;
            }
            string inFile = args[0];
            if (!File.Exists(inFile))
            {
                Console.WriteLine("Input file not existing");
                return 1;
            }
            string outFile = Path.ChangeExtension(inFile, ".lbl");
            if (!DecryptFile(inFile, outFile))
            {
                Console.WriteLine("Decryption failed");
                return 1;
            }
            return 0;
        }

        static bool DecryptFile(string inFile, string outFile)
        {
            try
            {
                using (FileStream fsRead = new FileStream(inFile, FileMode.Open))
                {
                    using (FileStream fsWrite = new FileStream(outFile, FileMode.Create))
                    {
                        for (; ; )
                        {
                            byte[] data = DecryptLine(fsRead);
                            if (data == null)
                            {
                                break;
                            }

                            for (int i = 0; i < data.Length; i++)
                            {
                                if (data[i] == 0)
                                {
                                    break;
                                }
                                fsWrite.WriteByte(data[i]);
                            }
                            fsWrite.WriteByte((byte)'\r');
                            fsWrite.WriteByte((byte)'\n');
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static byte[] DecryptLine(FileStream fs)
        {
            try
            {
                int lenH = fs.ReadByte();
                int lenL = fs.ReadByte();
                if (lenH < 0 || lenL < 0)
                {
                    return null;
                }

                int dataLen = (lenH << 8) + lenL;
                int remainder = 0;
                if ((dataLen % 8) != 0)
                {
                    remainder = 8 - dataLen % 8;
                }

                int blockSize = dataLen + remainder;
                byte[] data = new byte[blockSize + 8];
                int count = fs.Read(data, 0, blockSize + 2);
                if (count < blockSize + 2)
                {
                    return null;
                }
                UInt32[] buffer = new uint[(blockSize / sizeof(UInt32)) + 2];
                for (int i = 0; i < data.Length; i += sizeof(UInt32))
                {
                    buffer[i >> 2] = BitConverter.ToUInt32(data, i);
                }

                UInt32[] mask = new UInt32[2];
                mask[0] = 0x49BBDC0;
                mask[1] = 0x1AF70;
                if (!DecryptBlock(mask, buffer, 2))
                {
                    return null;
                }

                byte[] result = new byte[blockSize];
                for (int i = 0; i < blockSize; i += sizeof(UInt32))
                {
                    byte[] conf = BitConverter.GetBytes(buffer[i >> 2]);
                    Array.Copy(conf, 0, result, i, conf.Length);
                }

                return result;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static bool DecryptBlock(UInt32[] mask, UInt32[] buffer, int codeTable)
        {
            UInt32 code1;
            UInt32 code2;
            UInt32 code3;
            UInt32 code4;

            switch (codeTable)
            {
                case 1:
                    code1 = 0x87A32FEB;
                    code2 = 0x77539F1B;
                    code3 = 0x67030F4B;
                    code4 = 0x57B37F7B;
                    break;

                case 2:
                    code1 = 0xFA7E14D0;
                    code2 = 0x249B910E;
                    code3 = 0x2FDD6FFC;
                    code4 = 0x15834A78;
                    break;

                default:
                    code1 = 0x29B76A4;
                    code2 = 0xCB6DB50A;
                    code3 = 0x71395D29;
                    code4 = 0x0DBC09C2;
                    break;
            }

            UInt32 mask1 = mask[0];
            UInt32 mask2 = mask[1];

            int bufferSize = buffer.Length * sizeof(UInt32);
            int dataSize = bufferSize;
            dataSize &= ~0x07;
            if (dataSize <= 8)
            {
                return false;
            }
            int blockCount = ((dataSize - 9) >> 3) + 1;
            int dataPos = 0;
            for (int i = 0; i < blockCount; i++)
            {
                UInt32 data1 = buffer[dataPos + 0];
                UInt32 data2 = buffer[dataPos + 1];
                UInt32 data1Old = data1;
                UInt32 data2Old = data2;
                UInt32 codeDyn = 0xC6EF3720;

                for (int j = 0; j < 32; j++)
                {
                    data2 -= (data1 + codeDyn) ^ (code3 + 16 * data1) ^ (code4 + (data1 >> 5));
                    data1 -= (data2 + codeDyn) ^ (code1 + 16 * data2) ^ (code2 + (data2 >> 5));
                    codeDyn += 0x61C88647;
                }
                buffer[dataPos + 0] = data1 ^ mask1;
                buffer[dataPos + 1] = data2 ^ mask2;
                mask1 = data1Old;
                mask2 = data2Old;

                dataPos += 2;
            }

            return true;
        }
    }
}
