module TaskSeq.Extenions

open System
open Xunit
open FsUnit.Xunit
open FsToolkit.ErrorHandling

open FSharp.Control

//
// TaskSeq.except
// TaskSeq.exceptOfSeq
//


// module TaskBuilder =
//     open TaskSeq.Tests

//     [<Theory; ClassData(typeof<TestImmTaskSeq>)>]
//     let ``TaskSeq-existsAsync happy path last item of seq`` variant =
//         task {
//             let values =  Gen.getSeqImmutable variant
//             let mutable sum = 0
//             for x in values do
//                 sum <- sum + x
//         }
        // |> TaskSeq.existsAsync (fun x -> task { return x = 10 })
        // |> Task.map (should be True)
