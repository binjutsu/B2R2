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

  The above copyright notice and this permission notice shall be included in
  all copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
  THE SOFTWARE.
*)

module B2R2.Core.Tests.ByteArray

open Microsoft.VisualStudio.TestTools.UnitTesting

open B2R2

[<TestClass>]
type TestClass () =

  [<TestMethod>]
  member __.``ByteArray Test`` () =
    let hexString = "68656c6c6f"
    let newArray = ByteArray.ofHexString hexString
    CollectionAssert.AreEqual ([| 0x68uy; 0x65uy; 0x6cuy; 0x6cuy; 0x6fuy |], newArray)
    let hexString = "68656C6C6F"
    let newArray = ByteArray.ofHexString hexString
    CollectionAssert.AreEqual ([| 0x68uy; 0x65uy; 0x6cuy; 0x6cuy; 0x6fuy |], newArray)

  [<TestMethod>]
  member __.``CString Extraction Test`` () =
    let arr = [| 0x68uy; 0x65uy; 0x6cuy; 0x6cuy; 0x6fuy;
                 0x00uy; (* NULL character *)
                 0x68uy; 0x65uy; 0x6cuy; 0x6cuy; 0x6fuy; |]
    let str= ByteArray.extractCString arr 0
    Assert.AreEqual ("hello", str)

  [<TestMethod>]
  member __.``Pattern Matching Test`` () =
    let buf = "hellotexthellotexthellotexthellopencilfsharptesttext"B
    let pattern = "text"B
    let offset = uint64 0
    let indexList = ByteArray.findIdxs offset pattern buf
    Assert.AreEqual([48UL; 23UL; 14UL; 5UL], indexList)
