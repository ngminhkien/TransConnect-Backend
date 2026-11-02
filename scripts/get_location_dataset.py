"""Fetch and normalize the pinned Locations catalogue; never connects to a database."""

import argparse
import hashlib
import json
import re
import sys
import urllib.request
from pathlib import Path


SOURCE = "https://github.com/thanglequoc/vietnamese-provinces-database"
VERSION = "v5.2.0"
COMMIT = "62b1abdfe39625289c37a58ef6678e9db3d37f84"
ARTIFACT = "json/simplified_json_generated_data_vn_units.json"
HASHES = {
    ARTIFACT: "f624138116bde151c860cbd25a84cd6b9b38250e8a049ed371c55dee729ac286",
    "json/vn_provinces_metadata.json": "14f7815413702837918e9aa0961308b072e0d75b2bdf5b5865fc4a23cdd8eda4",
    "LICENSE": "0173d3551f805ee7fabad1948b1219e79dca42f5f93a1b07d4f9a12f9cbfb8da",
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def normalize(data):
    if not isinstance(data, list) or not data:
        raise ValueError("Expected a nonempty province array.")
    provinces, wards, province_codes, ward_codes = [], [], set(), set()
    for province in data:
        code = province.get("Code")
        name = province.get("FullName")
        if not isinstance(code, str) or not re.fullmatch(r"[0-9]{2}", code):
            raise ValueError("Province code must be a two-digit string.")
        if code in province_codes or not isinstance(name, str) or not name.strip():
            raise ValueError("Duplicate province code or missing province name.")
        province_codes.add(code)
        provinces.append({"code": code, "name": name})
        children = province.get("Wards")
        if not isinstance(children, list) or not children:
            raise ValueError("Province has no ward array.")
        for ward in children:
            ward_code, ward_name = ward.get("Code"), ward.get("FullName")
            if not isinstance(ward_code, str) or not re.fullmatch(r"[0-9]{5}", ward_code):
                raise ValueError("Ward code must be a five-digit string.")
            if ward_code in ward_codes or ward.get("ProvinceCode") != code:
                raise ValueError("Duplicate ward code or inconsistent parent province.")
            if not isinstance(ward_name, str) or not ward_name.strip():
                raise ValueError("Missing ward name.")
            ward_codes.add(ward_code)
            wards.append({"code": ward_code, "name": ward_name, "provinceCode": code})
    return sorted(provinces, key=lambda p: p["code"]), sorted(wards, key=lambda w: w["code"])


def write_bytes_atomic(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(data)
    temporary.replace(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-directory", type=Path,
                        default=Path(__file__).resolve().parents[1] / ".codex-build/locations" / VERSION)
    parser.add_argument("--source-directory", type=Path,
                        help="Read pinned source files from an existing checkout/cache without network access.")
    args = parser.parse_args()
    blobs = {}
    for relative, expected in HASHES.items():
        if args.source_directory:
            blob = (args.source_directory / relative).read_bytes()
        else:
            url = "https://raw.githubusercontent.com/thanglequoc/vietnamese-provinces-database/" + COMMIT + "/" + relative
            request = urllib.request.Request(url, headers={"User-Agent": "TransConnect-Locations-Setup/1.0"})
            with urllib.request.urlopen(request, timeout=60) as response:
                blob = response.read(5_000_001)
            if len(blob) > 5_000_000:
                raise ValueError("Source artifact exceeds the preparation size limit.")
        if digest(blob) != expected:
            raise ValueError("SHA-256 mismatch: " + relative + ". Keep the pinned source; do not bypass verification.")
        blobs[relative] = blob

    metadata = json.loads(blobs["json/vn_provinces_metadata.json"])
    if metadata.get("DatasetVersion") != VERSION:
        raise ValueError("Source metadata does not match the pinned version.")
    provinces, wards = normalize(json.loads(blobs[ARTIFACT]))
    # Counts describe this pinned artifact, not a permanent business rule.
    if (len(provinces), len(wards)) != (34, 3321):
        raise ValueError("Unexpected counts for the pinned release.")
    payload = {"source": SOURCE, "version": VERSION, "checksum": HASHES[ARTIFACT],
               "sourceCommit": COMMIT, "license": "MIT", "artifact": ARTIFACT,
               "provinces": provinces, "wards": wards}
    encoded = (json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    manifest = {"source": SOURCE, "version": VERSION, "commit": COMMIT,
                "sourceHashes": HASHES, "sourceMetadata": metadata,
                "license": "MIT", "provinceCount": len(provinces), "wardCount": len(wards),
                "normalizedFile": "locations-dataset.json", "normalizedSha256": digest(encoded),
                "normalizerVersion": 1,
                "note": "Counts and checksums verify this artifact, not administrative/legal currency or physical coordinates."}
    # Nothing is published until every source hash, field and relationship has passed.
    for relative, blob in blobs.items():
        write_bytes_atomic(args.output_directory / "source" / relative, blob)
    write_bytes_atomic(args.output_directory / "locations-dataset.json", encoded)
    write_bytes_atomic(args.output_directory / "manifest.json",
                       (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    print("Prepared", VERSION, "with", len(provinces), "provinces and", len(wards), "wards.")
    print("Import payload:", (args.output_directory / "locations-dataset.json").resolve())
    print("Normalized SHA-256:", digest(encoded))
    print("No database connection or import was performed.")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        print("Dataset preparation failed:", error, file=sys.stderr)
        sys.exit(1)
