(*
  B2R2 - the Next-Generation Reversing Platform

  Author: Sang Kil Cha <sangkilc@kaist.ac.kr>

  Copyright (c) SoftSec Lab. @ KAIST, since 2016

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
*)

module internal B2R2.BinFile.ELF.Helper

open System
open B2R2
open B2R2.BinFile

let [<Literal>] secPLT = ".plt"
let [<Literal>] secTEXT = ".text"

let private pltThumbStubBytes = [| 0x78uy; 0x47uy; 0xc0uy; 0x46uy |]

// FIXME
let pltFirstSkipBytes = function
| Arch.IntelX86
| Arch.IntelX64 -> 0x10UL
| Arch.ARMv7 -> 0x14UL
| Arch.AARCH64 -> 0x20UL
| _ -> failwith "Implement"

let isThumbPltELFSymbol sAddr (plt: ELFSection) (reader: BinReader) =
 let offset = Convert.ToInt32 (sAddr - plt.SecAddr + plt.SecOffset)
 reader.PeekBytes (4, offset) = pltThumbStubBytes

let findPltSize sAddr plt reader = function
  | Arch.IntelX86
  | Arch.IntelX64 -> 0x10UL
  | Arch.ARMv7 ->
    if isThumbPltELFSymbol sAddr plt reader then 0x10UL else 0x0CUL
  | Arch.AARCH64 -> 0x10UL
  | _ -> failwith "Implement"

let isFuncSymb s =
  s.SymType = SymbolType.STTFunc || s.SymType = SymbolType.STTGNUIFunc

let inline tryFindFuncSymb elf addr =
  match Map.tryFind addr elf.SymInfo.AddrToSymbTable with
  | None -> None
  | Some s -> if isFuncSymb s then Some s.SymName else None

let parsePLT arch sections (reloc: RelocInfo) reader =
  match Map.tryFind secPLT sections.SecByName with
  | Some plt ->
    let pltStartAddr = plt.SecAddr + pltFirstSkipBytes arch
    let folder (map, sAddr) _ (rel: RelocationEntry) =
      match rel.RelType with
      | RelocationX86 RelocationX86.Reloc386JmpSlot
      | RelocationX64 RelocationX64.RelocX64JmpSlot
      | RelocationARMv7 RelocationARMv7.RelocARMJmpSlot
      | RelocationARMv8 RelocationARMv8.RelocAARCH64JmpSlot ->
        let nextStartAddr = sAddr + findPltSize sAddr plt reader arch
        let addrRange = AddrRange (sAddr, nextStartAddr)
        ARMap.add addrRange rel.RelSymbol map, nextStartAddr
      | _ -> map, sAddr
    Map.fold folder (ARMap.empty, pltStartAddr) reloc.RelocByAddr |> fst
  | None -> ARMap.empty

let private parseELF offset reader =
  let eHdr = Header.parse reader offset
  let secs = Section.parse eHdr reader
  let proghdrs = ProgHeader.parse eHdr reader
  let loadableSegs = ProgHeader.getLoadableProgHeaders proghdrs
  let loadableSecNums = ProgHeader.getLoadableSecNums secs loadableSegs
  let symbs = Symbol.parse eHdr secs reader
  let reloc = Relocs.parse eHdr secs symbs reader
  let plt = parsePLT eHdr.MachineType secs reloc reader
  let symbs = Symbol.updatePLTSymbols symbs plt
  { ELFHdr = eHdr
    ProgHeaders = proghdrs
    LoadableSegments = loadableSegs
    LoadableSecNums = loadableSecNums
    SecInfo = secs
    SymInfo = symbs
    RelocInfo = reloc
    PLT = plt
    BinReader = reader }

let initELF bytes =
  let reader = BinReader.Init (bytes, Endian.Little)
  if Header.isELF reader 0 then ()
  else raise FileFormatMismatchException
  Header.peekEndianness reader 0
  |> BinReader.RenewReader reader
  |> parseELF 0

let secFlagToSectionKind flag entrySize =
  if flag &&& SectionFlag.SHFExecInstr = SectionFlag.SHFExecInstr then
    if entrySize > 0UL then SectionKind.LinkageTableSection
    else SectionKind.ExecutableSection
  elif flag &&& SectionFlag.SHFWrite = SectionFlag.SHFWrite then
    SectionKind.WritableSection
  else
    SectionKind.ExtraSection

let elfSectionToSection (sec: ELFSection) =
  { Address = sec.SecAddr
    Kind = secFlagToSectionKind sec.SecFlags sec.SecEntrySize
    Size = sec.SecSize
    Name = sec.SecName }

let rec isValid addr = function
  | seg :: tl ->
    let vAddr = seg.PHAddr
    if addr >= vAddr && addr < vAddr + seg.PHFileSize then true
    else isValid addr tl
  | [] -> false

let rec translateAddr addr = function
  | seg :: tl ->
    let vAddr = seg.PHAddr
    if addr >= vAddr && addr < vAddr + seg.PHFileSize then
      Convert.ToInt32 (addr - vAddr + seg.PHOffset)
    else translateAddr addr tl
  | [] -> raise InvalidAddrReadException

// vim: set tw=80 sts=2 sw=2:
