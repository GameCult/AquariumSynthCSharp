/*
  Test-only AquariumSynthCSharp ZynAddSubFX PAD oracle.

  This file links against the pinned ZynAddSubFX reference source in an ignored
  artifact build. It is development/parity tooling, not Aquarium runtime code.
*/

#include "../globals.h"
#include "../DSP/FFTwrapper.h"
#include "../Misc/Allocator.h"
#include "../Misc/Time.h"
#include "../Misc/Util.h"
#include "../Misc/XMLwrapper.h"
#include "../Params/Controller.h"
#include "../Params/PADnoteParameters.h"
#include "../Synth/OscilGen.h"
#include "../Synth/PADnote.h"
#include <algorithm>
#include <cmath>
#include <cstdio>
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

void normalizeMaxLikePad(float* values, int count)
{
    float max = 0.0f;
    for(int i = 0; i < count; ++i)
        if(values[i] > i)
            max = values[i];

    if(max > 0.000001f)
        for(int i = 0; i < count; ++i)
            values[i] /= max;
}

void writeHarmonics(
    PADnoteParameters& parameters,
    const SYNTH_T& synth,
    const std::string& path,
    float baseFrequency)
{
    std::vector<float> harmonics(synth.oscilsize, 0.0f);
    parameters.oscilgen->get(harmonics.data(), baseFrequency, false);
    normalizeMaxLikePad(harmonics.data(), synth.oscilsize / 2);
    writeFloat32(path, harmonics.data(), synth.oscilsize / 2);
}

void renderNote(
    const PADnoteParameters& parameters,
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
    PADnote note(&parameters, synthParams, 0, nullptr, nullptr, false);

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
        std::copy(left.begin(), left.begin() + count, rendered.begin() + written);
        written += count;
    }

    writeFloat32(path, rendered.data(), static_cast<int>(rendered.size()));
}

}

int main(int argc, char** argv)
{
    if(argc < 3 || argc > 7) {
        std::cerr << "Usage: ZynPadReference <input.xiz> <output.f32> [kit-index] [sample-index|note|harmonics] [frequency-hz] [seconds]\n";
        return 2;
    }

    const std::string inputPath = argv[1];
    const std::string outputPath = argv[2];
    const int kitIndex = argc >= 4 ? parseInt(argv[3], "kit index") : 0;
    const bool renderAudio = argc >= 5 && std::string(argv[4]) == "note";
    const bool renderHarmonics = argc >= 5 && std::string(argv[4]) == "harmonics";
    const int requestedSample = argc >= 5 && !renderAudio && !renderHarmonics ? parseInt(argv[4], "sample index") : 0;
    const float frequency = argc >= 6 ? std::strtof(argv[5], nullptr) : 261.6256f;
    const float seconds = argc >= 7 ? std::strtof(argv[6], nullptr) : 1.5f;

    SYNTH_T synth;
    synth.samplerate = 44100;
    synth.buffersize = 256;
    synth.oscilsize = 1024;
    synth.alias(false);

    AbsTime time(synth);
    FFTwrapper fft(synth.oscilsize);
    PADnoteParameters parameters(synth, &fft, &time);

    XMLwrapper xml;
    if(xml.loadXMLfile(inputPath) != 0) {
        std::cerr << "Could not load Zyn instrument: " << inputPath << "\n";
        return 2;
    }

    if(!enterInstrument(xml) ||
       !xml.enterbranch("INSTRUMENT_KIT") ||
       !xml.enterbranch("INSTRUMENT_KIT_ITEM", kitIndex) ||
       !xml.enterbranch("PAD_SYNTH_PARAMETERS")) {
        std::cerr << "Could not find PAD_SYNTH_PARAMETERS for kit item " << kitIndex
                  << " in " << inputPath << "\n";
        return 2;
    }

    parameters.getfromXML(xml);
    parameters.applyparameters([] { return false; }, 1);

    if(renderAudio) {
        renderNote(parameters, synth, time, outputPath, frequency, seconds);
        std::cout << "mode=note samples=" << static_cast<int>(seconds * synth.samplerate)
                  << " frequency=" << frequency << "\n";
        FFT_cleanup();
        return 0;
    }

    if(renderHarmonics) {
        writeHarmonics(parameters, synth, outputPath, frequency);
        std::cout << "mode=harmonics count=" << synth.oscilsize / 2
                  << " basefreq=" << frequency << "\n";
        FFT_cleanup();
        return 0;
    }

    int sampleIndex = requestedSample;
    if(sampleIndex < 0 || sampleIndex >= PAD_MAX_SAMPLES || parameters.sample[sampleIndex].smp == nullptr) {
        sampleIndex = -1;
        for(int i = 0; i < PAD_MAX_SAMPLES; ++i) {
            if(parameters.sample[i].smp != nullptr) {
                sampleIndex = i;
                break;
            }
        }
    }

    if(sampleIndex < 0) {
        std::cerr << "Zyn generated no PAD samples for " << inputPath << "\n";
        return 2;
    }

    const auto& sample = parameters.sample[sampleIndex];
    writeFloat32(outputPath, sample.smp, sample.size);

    const int preview = std::min(sample.size, 8);
    std::cout << "sampleIndex=" << sampleIndex
              << " size=" << sample.size
              << " basefreq=" << sample.basefreq
              << " preview=";
    for(int i = 0; i < preview; ++i) {
        if(i) std::cout << ",";
        std::cout << sample.smp[i];
    }
    std::cout << "\n";

    FFT_cleanup();
    return 0;
}
