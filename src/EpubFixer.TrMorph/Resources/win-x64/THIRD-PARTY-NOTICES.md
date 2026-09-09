# TRmorph runtime provenance

TRmorph is sourced from https://github.com/coltekin/TRmorph (MIT license),
commit `fbceb1133fbdc51bd63693d448b7cb75672e2256`. `trmorph.fst` was built
offline from that source with foma 0.9.18 sources. The x64 `flookup.exe` was
built from the same foma sources with MinGW-w64; it requires only Windows
system DLLs at runtime. The original foma source package is available at:
https://bitbucket.org/mhulden/foma/downloads/foma-0.9.18_win32.zip

The foma package is Apache-2.0 licensed; the accompanying `COPYING` file is
included with these resources. SHA-256 values for the bundled files are:

| File | SHA-256 |
| --- | --- |
| flookup.exe | 04C68F3B5AD0748A11616E994054121C8D61D79B36F2B6E7581C97146693A59A |
| trmorph.fst | ABB3272526BF77E8827B14919478773CBC7060906622BEB61EFFEDAA2AEBDCAD |
