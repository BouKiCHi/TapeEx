
Write-Output "--- test.ps1 ---"

function execute ($options) {
    ../../TapeEx.exe $options
}

rm .\output.wav
execute "-w","data/data01.txt"
execute "-r","output.wav"
execute "-o","result.wav","-w","output.txt"
