COCO = ..\..\Common\bin\Coco.exe

# "all" depends on 2 files, really (Parser.cs and Scanner.cs), but they
# are both generated in one go and I don't know a better way to tell
# nmake that.  --KRML
all: DeserializerParser.cs

DeserializerParser.cs: Scanner.frame Parser.frame Deserializer.atg
	$(COCO) Deserializer.atg
	copy parser.cs DeserializerParser.cs
	copy scanner.cs DeserializerScanner.cs

clean:
	rm -f DeserializerScanner.cs DeserializerParser.cs
