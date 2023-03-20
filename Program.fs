open System

open System.Linq
open System.Buffers
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open BenchmarkDotNet.Diagnosers
open BenchmarkDotNet.Diagnostics.Windows.Configs;
open BenchmarkDotNet.Order
open BenchmarkDotNet.Mathematics


[<Literal>] 
let arrayBuilderStartingSize = 4

[<Struct>]
[<NoEquality>]
[<NoComparison>]
type ArrayBuilder<'T> =
    {
        mutable currentCount: int
        mutable currentArray: 'T array
    }

let inline addToBuilder (item: 'T) (builder:byref<ArrayBuilder<'T>>) = 
    match builder.currentCount = builder.currentArray.Length with
    | false ->
        builder.currentArray[ builder.currentCount ] <- item
        builder.currentCount <- builder.currentCount + 1
    | true ->
        let newArr = Array.zeroCreate (builder.currentArray.Length * 2)
        builder.currentArray.CopyTo(newArr, 0)
        builder.currentArray <- newArr
        newArr[builder.currentCount] <- item
        builder.currentCount <- builder.currentCount + 1

let inline builderToArray (builder:inref<ArrayBuilder<'T>>) = 
    match builder.currentCount = builder.currentArray.Length with
    | true -> builder.currentArray
    | false -> builder.currentArray |> Array.truncate builder.currentCount

[<Struct>]
type InnerRecord = {X : int64; Y : float; Z : int64}


[<Literal>] 
let stackAllocSize = 8
[<Literal>] 
let stackAllocDouble = 16
[<Literal>] 
let stackAllocHuge = 50


[<MemoryDiagnoser>]
//[<DryJob>]  // Uncomment heere for quick local testing
type SeqToArrayBenchmark()   = 


    [<Params(0,1,2,3,4,5,6,7,8,9,10,16,20,50,512,513, Priority = 0)>] 
    member val NumberOfItems = -1 with get,set

    member val ItemsSequence = Unchecked.defaultof<seq<InnerRecord>> with get,set


    [<GlobalSetup>]
    member this.GlobalSetup () = 
        this.ItemsSequence <- Seq.init (this.NumberOfItems) (fun i -> {X = i; Y = float i; Z = int64 i})

    [<Benchmark()>]
    member this.CheatingOracle () = 
        use source = this.ItemsSequence.GetEnumerator()
        let arr = Array.init this.NumberOfItems (fun _ -> 
            ignore (source.MoveNext() )
            source.Current )
        arr

    [<Benchmark(Baseline = true )>]
    member this.NormalSeqToArray () = 
        Seq.toArray this.ItemsSequence

    [<Benchmark()>]
    member this.ManualBuilder () = 
        let mutable builder = {currentCount = 0; currentArray = Array.zeroCreate arrayBuilderStartingSize}
        for item in this.ItemsSequence do
            addToBuilder item &builder
        builderToArray &builder

    [<Benchmark()>]
    member this.ManualSpecialCase0 () =         
        use e = this.ItemsSequence.GetEnumerator()
        if e.MoveNext() then
            let arr = [|e.Current;Unchecked.defaultof<_>;Unchecked.defaultof<_>;Unchecked.defaultof<_>|]
            let mutable builder = {currentCount = 1; currentArray = arr}            
            while e.MoveNext() do
                addToBuilder e.Current &builder
            builderToArray &builder
        else Array.empty

    [<Benchmark()>]
    member this.ManualStackAllocUpTo8 () =     
        let stackFor8 = NativePtr.stackalloc<InnerRecord> stackAllocSize
        let mutable smallCounter = 0
        use e = this.ItemsSequence.GetEnumerator()

        while smallCounter < stackAllocSize && e.MoveNext() do
            NativePtr.set stackFor8 smallCounter (e.Current)
            smallCounter <- smallCounter + 1

        let arr = Array.init smallCounter (NativePtr.get stackFor8) 
        let mutable builder = {currentCount = smallCounter; currentArray = arr}            
        while e.MoveNext() do
            addToBuilder e.Current &builder
        builderToArray &builder

    //[<Benchmark()>]
    member this.ManualStackAllocUpTo16 () = 
        let stackFor8 = NativePtr.stackalloc<InnerRecord> stackAllocDouble
        let mutable smallCounter = 0
        use e = this.ItemsSequence.GetEnumerator()

        while smallCounter < stackAllocDouble && e.MoveNext() do
            NativePtr.set stackFor8 smallCounter (e.Current)
            smallCounter <- smallCounter + 1

        let arr = Array.init smallCounter (NativePtr.get stackFor8) 
        let mutable builder = {currentCount = smallCounter; currentArray = arr}            
        while e.MoveNext() do
            addToBuilder e.Current &builder
        builderToArray &builder

    //[<Benchmark()>]
    member this.StackAllocHuge () =  
        let stackFor8 = NativePtr.stackalloc<InnerRecord> stackAllocHuge
        let mutable smallCounter = 0
        use e = this.ItemsSequence.GetEnumerator()

        while smallCounter < stackAllocHuge && e.MoveNext() do
            NativePtr.set stackFor8 smallCounter (e.Current)
            smallCounter <- smallCounter + 1

        let arr = Array.init smallCounter (NativePtr.get stackFor8) 
        let mutable builder = {currentCount = smallCounter; currentArray = arr}            
        while e.MoveNext() do
            addToBuilder e.Current &builder
        builderToArray &builder




[<EntryPoint>]
let main argv = 
    BenchmarkRunner.Run<SeqToArrayBenchmark>() |> ignore   
    0