﻿namespace FSharp.BonsaiStore
module StoreBuilder =

    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Linq.QuotationEvaluation
    open FSharp.Collections.RangeMap
    
    module T = FSharp.BonsaiStore.Internal.Tree
    module F = FSharp.BonsaiStore.Internal.Filter
    module Q = FSharp.BonsaiStore.Quatations

    /// Build configuration.
    type BuildStoreConfiguration =
        {
            /// If set to true, cache all leaf nodes at each level.
            /// This may speed up reporting but increases memory footprint.
            UseCaching : bool
        }
        static member Default = {UseCaching = false}

    /// Create tree configuration
    let private buildTreeConf conf =
        let df = T.defaultBuildTreeConfiguration
        { new T.IBuildTreeConfiguration with 
            member this.CacheElements() = conf.UseCaching
            member this.BuildMap<'K,'V when 'K : comparison> xs : IRangeMap<'K,'V> = 
                df.BuildMap xs
        }
    
    /// Builds a store
    let buildStore<'T> (conf: BuildStoreConfiguration) (items: seq<'T>) : IStore<'T> =

        // Find indexes from type.
        let indexes = Q.extractIndexes<'T>()

        // Get levels.
        let levels =
            indexes
            |> List.sortBy fst
            |> List.mapi (fun i (l, exp) ->
                let exp = Expr.Cast(exp) : Expr<'T -> int>
                (i,  exp.Compile()())
            )

        // To filter function
        let toFilter (exp: Expr<'T -> bool>) = 
            Q.buildFilterGenerator<'T> (List.map snd indexes) exp

        // Build the store
        let tree = T.buildTree (buildTreeConf conf) levels items

        // Report function
        let report (filterExp: Expr<'T -> bool>) =
            let filterFun = Q.compileQuatationFilter filterExp
            let filter = toFilter filterExp
            fun (map: 'T -> 'R) (reduce: 'R [] -> 'R) ->
                let empty = reduce [||]
                let map x = if filterFun x then map x else empty
                F.report tree filter map reduce
        { new IStore<'T> 
            with member this.Report filter map reduce = report filter map reduce }

    /// Builds a store
    let buildDefaultStore<'T> (items: seq<'T>) : IStore<'T> =
        buildStore BuildStoreConfiguration.Default items