export LD_LIBRARY_PATH="/home/huji/.nuget/packages/opencvsharp4.official.runtime.linux-x64/4.11.0.20250507/runtimes/linux-x64/native/libOpenCvSharpExtern.so:${LD_LIBRARY_PATH}"

dotnet run --project NormalToHeightMapConverter.CLI -- -i images/7371-normal.jpg -o results/7371-normal.png
