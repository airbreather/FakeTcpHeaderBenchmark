# FakeTcpHeaderBenchmark
I wasn't happy with just what Sasha used in https://app.pluralsight.com/library/courses/making-dotnet-applications-faster for a particular benchmark:

1. The BCL types he used, `System.IO.BinaryReader` and `System.IO.MemoryStream` just aren't optimized for the cases he's interested in here.  Specifically, they're extremely defensive when it comes to reads that are potentially out of alignment, but then he goes to use some stuff that will fail in the cases that the BCL types will fail in.
2. He also claims that this overhead is due to "method calls", but this is unfair because it's the virtual method calls that really make it bad.
3. He goes on to implement a custom "generalized" solution in MSIL, but since the training was created, the corefx folks have made something very similar available on NuGet.

# Results
The methods are as follows:

1. `ReadHeadersBase` should be pretty much identical to Sasha's disaster case.
2. `ReadHeadersOptimized_StillVirtual` shows what can be done when you relax the alignment requirement while still basically keeping the same features that are present in the framework types.
3. `ReadHeadersOptimized_NonVirtual` shows the approximate limit of how fast you can go while still keeping separate method calls.
4. `ReadHeadersOptimized_ManuallyPumpedUnsafe` should be pretty much identical to Sasha's second-best case that uses `unsafe` and `fixed` in the consumer.
5. `ReadHeadersOptimized_MaximumPower` directly uses `System.Runtime.CompilerServices.Unsafe` to not only meet, but improve on Sasha's best-case generalized solution, most likely thanks to ref returns.

``` ini

BenchmarkDotNet=v0.10.1, OS=Microsoft Windows NT 6.2.9200.0
Processor=Intel(R) Core(TM) i7-6850K CPU 3.60GHz, ProcessorCount=12
Frequency=3515616 Hz, Resolution=284.4452 ns, Timer=TSC
  [Host]     : Clr 4.0.30319.42000, 64bit RyuJIT-v4.6.1586.0
  DefaultJob : Clr 4.0.30319.42000, 64bit RyuJIT-v4.6.1586.0


```
                                    Method |           Mean |     StdDev | Allocated |
------------------------------------------ |--------------- |----------- |---------- |
                           ReadHeadersBase | 31,341.9944 us | 18.8121 us |     512 B |
         ReadHeadersOptimized_StillVirtual | 14,976.2308 us | 43.7374 us |     512 B |
           ReadHeadersOptimized_NonVirtual |  3,571.2844 us | 14.8595 us |     128 B |
 ReadHeadersOptimized_ManuallyPumpedUnsafe |    508.3331 us |  1.7918 us |       0 B |
         ReadHeadersOptimized_MaximumPower |     95.4583 us |  0.0165 us |       0 B |
