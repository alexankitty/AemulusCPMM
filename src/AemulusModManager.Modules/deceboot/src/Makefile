ifeq ($(PLATFORM),)
	ifeq ($(OS),Windows_NT)
		PLATFORM = win-x64
	else ifeq ($(shell uname -s),Linux)
		PLATFORM = linux-x64
	else ifeq ($(shell uname -s),Darwin)
		PLATFORM = osx-x64
	endif
endif

ifeq ($(PLATFORM),win-x64)
	CXX = x86_64-w64-mingw32-g++
	CC = x86_64-w64-mingw32-gcc
else
	CXX = g++
	CC = gcc
endif

all: libkirk prxdecrypter main
	echo "Linking..."
	$(CXX) --std=c++11 main.o bn.o ec.o kirk_engine.o SHA1.o AES.o amctrl.o PrxDecrypter.o -Wall -Os --static -o deceboot

main:
	echo "Compiling main.cpp for $(PLATFORM)..."
	$(CXX) --std=c++11 -c main.cpp -Wall -Os
	
prxdecrypter:
	echo "Compiling PrxDecrypter.cpp for $(PLATFORM)..."
	$(CXX) -I. --std=c++11 -c Core/ELF/PrxDecrypter.cpp -Os
	
libkirk:
	echo "Compiling libkirk for $(PLATFORM)..."
	$(CC) -c ext/libkirk/*.c -Os
	
clean:
	rm -f *.o deceboot.exe deceboot
