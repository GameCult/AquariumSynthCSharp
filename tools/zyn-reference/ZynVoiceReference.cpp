/*
  Test-only AquaSynth ZynAddSubFX ADD voice oracle.

  This file links against the pinned ZynAddSubFX reference source in an ignored
  artifact build. It is development/parity tooling, not AquaSynth runtime code.
*/

#include "../globals.h"
#include "../DSP/FFTwrapper.h"
#include "../Misc/Allocator.h"
#include "../Misc/Time.h"
#include "../Misc/Util.h"
#include "../Misc/XMLwrapper.h"
#include "../Params/ADnoteParameters.h"
#include "../Params/Controller.h"
#include "../Synth/ADnote.h"
#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <fstream>
#include <iostream>
#include <string>
#include <vector>

using namespace zyn;

namespace {

bool enterInstrument(XMLwrapper& xml)
{
    if(xml.enterbranch("INSTRUMENT"))
        return true;

    if(xml.enterbranch("MASTER")) {
        if(xml.enterbranch("PART", 0) && xml.enterbranch("INSTRUMENT"))
            return true;
    }

    return false;
}

int parseInt(const char* text, const char* name)
{
    char* end = nullptr;
    const long value = std::strtol(text, &end, 10);
    if(end == text || *end != '\0') {
        std::cerr << "Invalid " << name << ": " << text << "\n";
        std::exit(2);
    }

    return static_cast<int>(value);
}

void writeFloat32(const std::string& path, const float* samples, int count)
{
    std::ofstream output(path, std::ios::binary);
    if(!output) {
        std::cerr << "Could not open output file: " << path << "\n";
        std::exit(2);
    }

    output.write(reinterpret_cast<const char*>(samples), sizeof(float) * count);
    if(!output) {
        std::cerr << "Could not write output file: " << path << "\n";
        std::exit(2);
    }
}

void renderAddNote(
    ADnoteParameters& parameters,
    const SYNTH_T& synth,
    const AbsTime& time,
    const std::string& path,
    float frequency,
    float seconds)
{
    Alloc memory;
    Controller controller(synth, &time);
    const float log2Frequency = std::log2(frequency);
    SynthParams synthParams{memory, controller, synth, time, 120, nullptr, log2Frequency, false, prng()};
    ADnote note(&parameters, synthParams, nullptr, nullptr, false);

    const int total = std::max(1, static_cast<int>(seconds * synth.samplerate));
    std::vector<float> rendered(total);
    std::vector<float> left(synth.buffersize);
    std::vector<float> right(synth.buffersize);
    int written = 0;
    while(written < total) {
        std::fill(left.begin(), left.end(), 0.0f);
        std::fill(right.begin(), right.end(), 0.0f);
        note.noteout(left.data(), right.data());
        const int count = std::min(synth.buffersize, total - written);
        for(int i = 0; i < count; ++i)
            rendered[written + i] = 0.5f * (left[i] + right[i]);
        written += count;
    }

    writeFloat32(path, rendered.data(), static_cast<int>(rendered.size()));
}

}

int main(int argc, char** argv)
{
    if(argc < 3 || argc > 6) {
        std::cerr << "Usage: ZynVoiceReference <input.xiz> <output.f32> [kit-index] [frequency-hz] [seconds]\n";
        return 2;
    }

    const std::string inputPath = argv[1];
    const std::string outputPath = argv[2];
    const int kitIndex = argc >= 4 ? parseInt(argv[3], "kit index") : 0;
    const float frequency = argc >= 5 ? std::strtof(argv[4], nullptr) : 110.0f;
    const float seconds = argc >= 6 ? std::strtof(argv[5], nullptr) : 1.8f;

    SYNTH_T synth;
    synth.samplerate = 44100;
    synth.buffersize = 256;
    synth.oscilsize = 1024;
    synth.alias(false);

    AbsTime time(synth);
    FFTwrapper fft(synth.oscilsize);
    ADnoteParameters parameters(synth, &fft, &time);

    XMLwrapper xml;
    if(xml.loadXMLfile(inputPath) != 0) {
        std::cerr << "Could not load Zyn instrument: " << inputPath << "\n";
        return 2;
    }

    if(!enterInstrument(xml) ||
       !xml.enterbranch("INSTRUMENT_KIT") ||
       !xml.enterbranch("INSTRUMENT_KIT_ITEM", kitIndex) ||
       !xml.enterbranch("ADD_SYNTH_PARAMETERS")) {
        std::cerr << "Could not find ADD_SYNTH_PARAMETERS for kit item " << kitIndex
                  << " in " << inputPath << "\n";
        return 2;
    }

    parameters.getfromXML(xml);
    renderAddNote(parameters, synth, time, outputPath, frequency, seconds);
    std::cout << "mode=add-note samples=" << static_cast<int>(seconds * synth.samplerate)
              << " frequency=" << frequency
              << " kit=" << kitIndex << "\n";

    FFT_cleanup();
    return 0;
}
