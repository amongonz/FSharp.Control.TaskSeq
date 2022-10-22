module FSharpy.Tests.``Bug #42 -- asynchronous`` // see PR #42

open System
open System.Threading.Tasks
open System.Diagnostics
open System.Collections.Generic

open Xunit
open FsUnit.Xunit
open FsToolkit.ErrorHandling

open FSharpy

// Module contains same tests as its previous file
// except that each item is delayed randomly to force
// an async Await behavior.

let getEmptyVariant variant : IAsyncEnumerable<int> =
    match variant with
    | "do" -> taskSeq { do! delayRandom () }
    | "do!" -> taskSeq { do! task { return! delayRandom () } } // TODO: this doesn't work with Task, only Task<unit>...
    | "yield! (seq)" -> taskSeq {
        do! delayRandom ()
        yield! Seq.empty<int>
      }
    | "yield! (taskseq)" -> taskSeq { yield! taskSeq { do! delayRandom () } }
    | _ -> failwith "Uncovered variant of test"


[<Fact>]
let ``CE empty taskSeq with MoveNextAsync -- untyped`` () = task {
    let tskSeq = taskSeq { do! delayRandom () }

    Assert.IsAssignableFrom<IAsyncEnumerable<obj>>(tskSeq)
    |> ignore

    do! moveNextAndCheck false (tskSeq.GetAsyncEnumerator())
}

[<Theory; InlineData "do"; InlineData "do!"; InlineData "yield! (seq)"; InlineData "yield! (taskseq)">]
let ``CE empty taskSeq with MoveNextAsync -- typed`` variant = task {
    let tskSeq = getEmptyVariant variant

    Assert.IsAssignableFrom<IAsyncEnumerable<int>>(tskSeq)
    |> ignore

    do! moveNextAndCheck false (tskSeq.GetAsyncEnumerator())
}

[<Theory; InlineData "do"; InlineData "do!"; InlineData "yield! (seq)"; InlineData "yield! (taskseq)">]
let ``CE  empty taskSeq, GetAsyncEnumerator multiple times`` variant = task {
    let tskSeq = getEmptyVariant variant
    use _e = tskSeq.GetAsyncEnumerator()
    use _e = tskSeq.GetAsyncEnumerator()
    use _e = tskSeq.GetAsyncEnumerator()
    ()
}

// Note: this test used to hang (#42), please leave it in, no matter how silly it looks
[<Theory; InlineData "do"; InlineData "do!"; InlineData "yield! (seq)"; InlineData "yield! (taskseq)">]
let ``CE  empty taskSeq, GetAsyncEnumerator multiple times and then MoveNextAsync`` variant = task {
    let tskSeq = getEmptyVariant variant
    use enumerator = tskSeq.GetAsyncEnumerator()
    use enumerator = tskSeq.GetAsyncEnumerator()
    do! moveNextAndCheck false enumerator
}

// Note: this test used to cause xUnit to crash (#42), please leave it in, no matter how silly it looks
[<Theory; InlineData "do"; InlineData "do!"; InlineData "yield! (seq)"; InlineData "yield! (taskseq)">]
let ``CE empty taskSeq, GetAsyncEnumerator + MoveNextAsync multiple times`` variant = task {
    let tskSeq = getEmptyVariant variant
    use enumerator1 = tskSeq.GetAsyncEnumerator()
    do! moveNextAndCheck false enumerator1

    // getting the enumerator again
    use enumerator2 = tskSeq.GetAsyncEnumerator()
    do! moveNextAndCheck false enumerator1 // original should still work without raising
    do! moveNextAndCheck false enumerator2 // new hone should also work without raising
}

// Note: this test used to cause xUnit to crash (#42), please leave it in, no matter how silly it looks
[<Theory; InlineData "do"; InlineData "do!"; InlineData "yield! (seq)"; InlineData "yield! (taskseq)">]
let ``CE empty taskSeq, GetAsyncEnumerator + MoveNextAsync in a loop`` variant = task {
    let tskSeq = getEmptyVariant variant

    // let's get the enumerator a few times
    for i in 0..100 do
        use enumerator = tskSeq.GetAsyncEnumerator()
        do! moveNextAndCheck false enumerator // these are all empty
}

[<Theory; InlineData "do"; InlineData "do!"; InlineData "yield! (seq)"; InlineData "yield! (taskseq)">]
let ``CE empty taskSeq, call Current before MoveNextAsync`` variant = task {
    let tskSeq = getEmptyVariant variant
    let enumerator = tskSeq.GetAsyncEnumerator()

    // call Current *before* MoveNextAsync
    let current = enumerator.Current
    current |> should equal 0 // we return Unchecked.defaultof, which is Zero in the case of an integer
}

[<Theory; InlineData "do"; InlineData "do!"; InlineData "yield! (seq)"; InlineData "yield! (taskseq)">]
let ``CE empty taskSeq, call Current after MoveNextAsync returns false`` variant = task {
    let tskSeq = getEmptyVariant variant
    let enumerator = tskSeq.GetAsyncEnumerator()
    do! moveNextAndCheck false enumerator // false for empty seq

    // call Current *after* MoveNextAsync returns false
    enumerator.Current |> should equal 0 // we return Unchecked.defaultof, which is Zero in the case of an integer
}

[<Fact>]
let ``CE taskSeq, call Current before MoveNextAsync`` () = task {
    let tskSeq = taskSeq {
        do! delayRandom ()
        yield "foo"
        do! delayRandom ()
        yield "bar"
    }

    let enumerator = tskSeq.GetAsyncEnumerator()

    // call Current before MoveNextAsync
    let current = enumerator.Current
    current |> should be Null // we return Unchecked.defaultof
}

[<Fact>]
let ``CE taskSeq, call Current after MoveNextAsync returns false`` () = task {
    let tskSeq = taskSeq {
        do! delayRandom ()
        yield "foo"
        do! delayRandom ()
        yield "bar"
    }

    let enum = tskSeq.GetAsyncEnumerator()
    do! moveNextAndCheck true enum // first item
    do! moveNextAndCheck true enum // second item
    do! moveNextAndCheck false enum // third item: false

    // call Current *after* MoveNextAsync returns false
    enum.Current |> should be Null // we return Unchecked.defaultof
}

[<Fact>]
let ``CE taskSeq, MoveNext once too far`` () = task {
    let tskSeq = taskSeq {
        do! delayRandom ()
        yield 1
        do! delayRandom ()
        yield 2
    }

    let enum = tskSeq.GetAsyncEnumerator()
    do! moveNextAndCheck true enum // first item
    do! moveNextAndCheck true enum // second item
    do! moveNextAndCheck false enum // third item: false
    do! moveNextAndCheck false enum // this used to be an error, see issue #39 and PR #42
}

[<Fact>]
let ``CE taskSeq, MoveNext too far`` () = task {
    let tskSeq = taskSeq {
        do! delayRandom ()
        yield Guid.NewGuid()
        do! delayRandom ()
        yield Guid.NewGuid()
    }

    // let's call MoveNext multiple times on an empty sequence
    let enum = tskSeq.GetAsyncEnumerator()

    // first get past the post
    do! moveNextAndCheck true enum // first item
    do! moveNextAndCheck true enum // second item
    do! moveNextAndCheck false enum // third item: false

    // then call it bunch of times to ensure we don't get an InvalidOperationException, see issue #39 and PR #42
    for i in 0..100 do
        do! moveNextAndCheck false enum

    // after whatever amount of time MoveNextAsync, we can still safely call Current
    enum.Current |> should equal Guid.Empty // we return Unchecked.defaultof, which is Guid.Empty for guids
}

// Note: this test used to cause xUnit to crash (#42), please leave it in, no matter how silly it looks
[<Fact>]
let ``CE taskSeq, call GetAsyncEnumerator twice, both should have equal behavior`` () = task {
    let tskSeq = taskSeq {
        do! delayRandom ()
        yield 1
        do! delayRandom ()
        yield 2
    }

    let enum1 = tskSeq.GetAsyncEnumerator()
    let enum2 = tskSeq.GetAsyncEnumerator()

    // enum1
    do! moveNextAndCheckCurrent true 1 enum1 // first item
    do! moveNextAndCheckCurrent true 2 enum1 // second item
    do! moveNextAndCheckCurrent false 0 enum1 // third item: false
    do! moveNextAndCheckCurrent false 0 enum1 // this used to be an error, see issue #39 and PR #42

    // enum2
    do! moveNextAndCheckCurrent true 1 enum2 // first item
    do! moveNextAndCheckCurrent true 2 enum2 // second item
    do! moveNextAndCheckCurrent false 0 enum2 // third item: false
    do! moveNextAndCheckCurrent false 0 enum2 // this used to be an error, see issue #39 and PR #42
}

// Note: this test used to cause xUnit to crash (#42), please leave it in, no matter how silly it looks
[<Fact>]
let ``CE taskSeq, cal GetAsyncEnumerator twice -- in lockstep`` () = task {
    let tskSeq = taskSeq {
        do! delayRandom ()
        yield 1
        do! delayRandom ()
        yield 2
    }

    let enum1 = tskSeq.GetAsyncEnumerator()
    let enum2 = tskSeq.GetAsyncEnumerator()

    // enum1 & enum2 in lock step
    do! moveNextAndCheckCurrent true 1 enum1 // first item
    do! moveNextAndCheckCurrent true 1 enum2 // first item

    do! moveNextAndCheckCurrent true 2 enum1 // second item
    do! moveNextAndCheckCurrent true 2 enum2 // second item

    do! moveNextAndCheckCurrent false 0 enum1 // third item: false
    do! moveNextAndCheckCurrent false 0 enum2 // third item: false

    do! moveNextAndCheckCurrent false 0 enum1 // this used to be an error, see issue #39 and PR #42
    do! moveNextAndCheckCurrent false 0 enum2 // this used to be an error, see issue #39 and PR #42
}

// Note: this test used to cause xUnit to crash (#42), please leave it in, no matter how silly it looks
[<Fact>]
let ``CE taskSeq, call GetAsyncEnumerator twice -- after full iteration`` () = task {
    let tskSeq = taskSeq {
        yield 1
        do! delayRandom ()
        yield 2
    }

    // enum1
    let enum1 = tskSeq.GetAsyncEnumerator()
    do! moveNextAndCheckCurrent true 1 enum1 // first item
    do! moveNextAndCheckCurrent true 2 enum1 // second item
    do! moveNextAndCheckCurrent false 0 enum1 // third item: false
    do! moveNextAndCheckCurrent false 0 enum1 // this used to be an error, see issue #39 and PR #42

    // enum2
    let enum2 = tskSeq.GetAsyncEnumerator()
    do! moveNextAndCheckCurrent true 1 enum2 // first item
    do! moveNextAndCheckCurrent true 2 enum2 // second item
    do! moveNextAndCheckCurrent false 0 enum2 // third item: false
    do! moveNextAndCheckCurrent false 0 enum2 // this used to be an error, see issue #39 and PR #42
}

// Note: this test used to hang (#42), please leave it in, no matter how silly it looks
[<Fact>]
let ``CE taskSeq, call GetAsyncEnumerator twice -- random mixed iteration`` () = task {
    let tskSeq = taskSeq {
        yield 1
        do! delayRandom ()
        yield 2
        do! delayRandom ()
        yield 3
    }

    // enum1
    let enum1 = tskSeq.GetAsyncEnumerator()

    // move #1
    do! moveNextAndCheckCurrent true 1 enum1 // first item

    // enum2
    let enum2 = tskSeq.GetAsyncEnumerator()
    enum1.Current |> should equal 1 // remains the same
    enum2.Current |> should equal 0 // should be at default location

    // move #2
    do! moveNextAndCheckCurrent true 1 enum2
    enum1.Current |> should equal 1
    enum2.Current |> should equal 1

    // move #2
    do! moveNextAndCheckCurrent true 2 enum2
    enum1.Current |> should equal 1
    enum2.Current |> should equal 2

    // move #1
    do! moveNextAndCheckCurrent true 2 enum1
    enum1.Current |> should equal 2
    enum2.Current |> should equal 2

    // move #1
    do! moveNextAndCheckCurrent true 3 enum1
    enum1.Current |> should equal 3
    enum2.Current |> should equal 2

    // move #1
    do! moveNextAndCheckCurrent false 0 enum1
    enum1.Current |> should equal 0
    enum2.Current |> should equal 2

    // move #2
    do! moveNextAndCheckCurrent true 3 enum2
    enum1.Current |> should equal 0
    enum2.Current |> should equal 3

    // move #2
    do! moveNextAndCheckCurrent false 0 enum2
    enum1.Current |> should equal 0
}

// Note: this test used to hang (#42), please leave it in, no matter how silly it looks
[<Fact>]
let ``TaskSeq-toArray can be applied multiple times to the same sequence`` () =
    let tq = taskSeq {
        yield! [ 1..3 ]
        do! delayRandom ()
        yield! [ 4..7 ]
        do! delayRandom ()
    }

    let (results1: _[]) = tq |> TaskSeq.toArray
    let (results2: _[]) = tq |> TaskSeq.toArray
    let (results3: _[]) = tq |> TaskSeq.toArray
    let (results4: _[]) = tq |> TaskSeq.toArray
    results1 |> should equal [| 1..7 |]
    results2 |> should equal [| 1..7 |] // no mutable state in taskSeq, multi iter remains stable
    results3 |> should equal [| 1..7 |] // id
    results4 |> should equal [| 1..7 |] // id

// Note: this test used to hang (#42), please leave it in, no matter how silly it looks
[<Fact>]
let ``TaskSeq-toArray can be applied multiple times to the same sequence -- mutable state`` () =
    let mutable before, middle, after = (0, 0, 0)

    let tq = taskSeq {
        before <- before + 1
        yield before
        yield! [ 100..120 ]
        do! delayRandom ()
        middle <- middle + 1
        yield middle
        yield! [ 100..120 ]
        do! delayRandom ()
        after <- after + 1
        yield after
    }

    let (results1: _ list) = tq |> TaskSeq.toList
    let (results2: _ list) = tq |> TaskSeq.toList
    let (results3: _ list) = tq |> TaskSeq.toList
    let (results4: _ list) = tq |> TaskSeq.toList

    let expectMutatedTo a = (a :: [ 100..120 ] @ [ a ] @ [ 100..120 ] @ [ a ])
    results1 |> should equal (expectMutatedTo 1)
    results2 |> should equal (expectMutatedTo 2)
    results3 |> should equal (expectMutatedTo 3)
    results4 |> should equal (expectMutatedTo 4)