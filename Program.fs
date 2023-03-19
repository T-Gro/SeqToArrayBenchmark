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


[<Struct>]
[<NoEquality>]
[<NoComparison>]
type ArrayBuilder<'T> = {mutable currentCount : int; mutable currentArray : 'T array}
    with
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member this.Add(item:'T) = 
            match this.currentCount with          
            | x when x < this.currentArray.Length ->
                this.currentArray[this.currentCount] <- item
                this.currentCount <- this.currentCount + 1
            | _ ->
                let newSize = this.currentArray.Length * 2
                let newArr = Array.zeroCreate newSize
                this.currentArray.CopyTo(newArr,0)        
                this.currentArray <- newArr
                newArr[this.currentCount] <- item
                this.currentCount <- this.currentCount + 1

        member this.ToArray() =
            match this.currentCount with         
            | x when this.currentCount = this.currentArray.Length -> this.currentArray
            | _ ->
                let finalArr = this.currentArray |> Array.truncate this.currentCount
                finalArr

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


    [<Params(0,1,2,3,4,5,6,7,8,9,10,16,20,50,100,500,1024,1025,100_000, Priority = 0)>] 
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
        let builder = {currentCount = 0; currentArray = Array.zeroCreate 4}
        for item in this.ItemsSequence do
            builder.Add(item)
        builder.ToArray()

    [<Benchmark()>]
    member this.ManualSpecialCase0 () =         
        use e = this.ItemsSequence.GetEnumerator()
        if e.MoveNext() then
            let arr = [|e.Current;Unchecked.defaultof<_>;Unchecked.defaultof<_>;Unchecked.defaultof<_>|]
            let builder = {currentCount = 1; currentArray = arr}            
            while e.MoveNext() do
                builder.Add(e.Current)
            builder.ToArray()
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
        let builder = {currentCount = smallCounter; currentArray = arr}            
        while e.MoveNext() do
            builder.Add(e.Current)
        builder.ToArray()

    [<Benchmark()>]
    member this.ManualStackAllocUpTo16 () = 
        let stackFor8 = NativePtr.stackalloc<InnerRecord> stackAllocDouble
        let mutable smallCounter = 0
        use e = this.ItemsSequence.GetEnumerator()

        while smallCounter < stackAllocDouble && e.MoveNext() do
            NativePtr.set stackFor8 smallCounter (e.Current)
            smallCounter <- smallCounter + 1

        let arr = Array.init smallCounter (NativePtr.get stackFor8) 
        let builder = {currentCount = smallCounter; currentArray = arr}            
        while e.MoveNext() do
            builder.Add(e.Current)
        builder.ToArray()

    [<Benchmark()>]
    member this.StackAllocHuge () =  
        let stackFor8 = NativePtr.stackalloc<InnerRecord> stackAllocHuge
        let mutable smallCounter = 0
        use e = this.ItemsSequence.GetEnumerator()

        while smallCounter < stackAllocHuge && e.MoveNext() do
            NativePtr.set stackFor8 smallCounter (e.Current)
            smallCounter <- smallCounter + 1

        let arr = Array.init smallCounter (NativePtr.get stackFor8) 
        let builder = {currentCount = smallCounter; currentArray = arr}            
        while e.MoveNext() do
            builder.Add(e.Current)
        builder.ToArray()




[<EntryPoint>]
let main argv = 
    BenchmarkRunner.Run<SeqToArrayBenchmark>() |> ignore   
    0