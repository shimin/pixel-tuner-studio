// Some tests switch the app (Product.Current) for a moment; they must not overlap with others.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
