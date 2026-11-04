// Generates the static results site into docs-site/ from the repo's own files. Needs only Node. Run from anywhere:
//   node static-demo/build.mjs
// Test figures come from README.md, the SOAP traffic from the golden files captured off the real net48 service, the diffs from the source.
import { cpSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { esc, page, startOutput } from "./kit/shell.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..");
const out = join(root, "docs-site");
startOutput(out, join(here, "kit"));
cpSync(join(here, "site.css"), join(out, "site.css"));
cpSync(join(here, "site.js"), join(out, "site.js"));

const read = (...p) => readFileSync(join(root, ...p), "utf8").replace(/\r\n/g, "\n");
const inline = (t) => esc(t).replace(/`([^`]+)`/g, '<code class="mono">$1</code>').replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");

const readme = read("README.md");
const tableAfter = (heading) => {
  const lines = readme.split("\n");
  let i = lines.findIndex((l) => l.startsWith(heading));
  while (!lines[i].startsWith("|")) i++;
  const rows = [];
  for (; lines[i]?.startsWith("|"); i++) rows.push(lines[i].split("|").slice(1, -1).map((c) => c.trim()));
  return { head: rows[0], rows: rows.slice(2) };
};
const latest = /Latest run: \*\*(\d+) tests, (\d+) passed, (\d+) failed, (\d+) skipped\*\*/.exec(readme);
if (!latest) throw new Error("README no longer states the latest test run in the expected form.");
const [, total, passed, failedCount, skipped] = latest;

const tested = tableAfter("| What is tested");
const kept = tableAfter("| Kept in SOAP");
if (tested.rows.reduce((sum, r) => sum + Number(r[2]), 0) !== Number(total)) throw new Error("The README's test table does not add up to its total.");

// Golden files

const goldenDir = ["modern", "tests", "Harbour.Consignments.Tests", "Golden"];
const names = [...new Set(readdirSync(join(root, ...goldenDir)).filter((f) => f.endsWith(".request.xml")).map((f) => f.replace(".request.xml", "")))].sort();
const pretty = (xml) => {
  let depth = 0;
  return xml.replace(/>\s*</g, ">\n<").split("\n").map((line) => {
    if (/^<\//.test(line)) depth--;
    const out = "  ".repeat(Math.max(depth, 0)) + line;
    if (/^<[^/?!][^>]*[^/]>$/.test(line) && !/<\//.test(line)) depth++;
    return out;
  }).join("\n");
};
const golden = names.map((name) => ({
  name,
  label: name.replace(/^(\d+)-/, "$1 ").replace(/-/g, " "),
  status: read(...goldenDir, `${name}.status.txt`).trim(),
  request: pretty(read(...goldenDir, `${name}.request.xml`).trim()),
  response: pretty(read(...goldenDir, `${name}.response.xml`).trim()),
}));
writeFileSync(join(out, "golden.json"), JSON.stringify(golden));

// Line diff of the real source files (longest common subsequence)

function diff(a, b) {
  const x = a.split("\n");
  const y = b.split("\n");
  const table = Array.from({ length: x.length + 1 }, () => new Int32Array(y.length + 1));
  for (let i = x.length - 1; i >= 0; i--) for (let j = y.length - 1; j >= 0; j--) {
    table[i][j] = x[i].trim() === y[j].trim() ? table[i + 1][j + 1] + 1 : Math.max(table[i + 1][j], table[i][j + 1]);
  }
  const lines = [];
  let i = 0;
  let j = 0;
  while (i < x.length || j < y.length) {
    if (i < x.length && j < y.length && x[i].trim() === y[j].trim()) { lines.push([" ", y[j]]); i++; j++; }
    else if (j < y.length && (i === x.length || table[i][j + 1] >= table[i + 1][j])) { lines.push(["+", y[j]]); j++; }
    else { lines.push(["-", x[i]]); i++; }
  }
  return lines;
}

function diffBlock(title, note, legacyPath, modernPath) {
  const lines = diff(read(...legacyPath.split("/")).trim(), read(...modernPath.split("/")).trim());
  const removed = lines.filter((l) => l[0] === "-").length;
  const added = lines.filter((l) => l[0] === "+").length;
  const same = lines.filter((l) => l[0] === " ").length;
  return `<div class="diff-block">
    <h3>${esc(title)}</h3>
    <p class="hint">${esc(note)}</p>
    <p class="mono diff-paths">- ${esc(legacyPath)}<br>+ ${esc(modernPath)}<br>${same} lines the same, ${removed} removed, ${added} added, ignoring indentation</p>
    <pre class="diff">${lines.map(([mark, text]) => `<span class="${mark === "+" ? "add" : mark === "-" ? "del" : "same"}">${mark} ${esc(text)}</span>`).join("\n")}</pre>
  </div>`;
}

const table = ({ head, rows }, numeric = []) => `<div class="table-wrap"><table>
  <thead><tr>${head.map((h, i) => `<th${numeric.includes(i) ? ' class="num"' : ""}>${inline(h)}</th>`).join("")}</tr></thead>
  <tbody>${rows.map((r) => `<tr>${r.map((c, i) => `<td${numeric.includes(i) ? ' class="num"' : ""}>${inline(c)}</td>`).join("")}</tr>`).join("")}</tbody>
</table></div>`;

const body = `
<section class="intro">
  <h1>WCF to modern .NET, without breaking a single client</h1>
  <p>A .NET Framework 4.8 WCF service moved to ASP.NET Core on .NET 10. The real compiled legacy client was pointed at the new host by changing one address in its config file, and its output is identical, line for line. Generated from the repo's README results, golden files and source.</p>
</section>
<section class="tally" aria-label="Latest test run">
  <div><strong>${total}</strong><span>Tests</span></div>
  <div><strong class="pass">${passed}</strong><span>Passed</span></div>
  <div><strong>${failedCount}</strong><span>Failed</span></div>
  <div><strong>${skipped}</strong><span>Skipped</span></div>
  <div><strong>${golden.length}</strong><span>Recorded SOAP exchanges</span></div>
</section>
<section class="block">
  <h2>What is tested</h2>
  ${table(tested, [2])}
</section>
<section class="block">
  <h2>The legacy client, unchanged</h2>
  <p class="hint lead-note">The transcript from the compiled net48 client. A test runs the same exe against the new host and requires this output.</p>
  <pre>${esc(read(...goldenDir, "legacy-client-transcript.txt").trim())}</pre>
</section>
<section class="block">
  <h2>Recorded SOAP traffic</h2>
  <p class="hint lead-note">Captured from the real net48 service. Each request is replayed against CoreWCF and its response compared with what the old service sent, including faults and nulls.</p>
  <label class="picker">Exchange <select id="golden-pick"></select></label>
  <p class="mono golden-status">HTTP status from the legacy service: <span id="golden-status"></span></p>
  <div class="pair">
    <div><h3>Request</h3><pre id="golden-request"></pre></div>
    <div><h3>Legacy response, the contract to match</h3><pre id="golden-response"></pre></div>
  </div>
</section>
<section class="block">
  <h2>What was kept, and why</h2>
  ${table(kept)}
</section>
<section class="block">
  <h2>The code, before and after</h2>
  ${diffBlock("The data contract", "The wire contract is copied across untouched, typo included. Differences are the namespaces and the runtime's attributes.", "legacy/Harbour.Legacy.Service/Contracts.cs", "modern/src/Harbour.Consignments.Web/Soap/Contracts.cs")}
  ${diffBlock("The service class", "Rules and storage moved out of the WCF class into a plain application layer. What remains is a thin adapter.", "legacy/Harbour.Legacy.Service/ConsignmentService.cs", "modern/src/Harbour.Consignments.Web/Soap/ConsignmentSoapService.cs")}
</section>
<footer class="foot">All data and names are invented. This is a results site: running the old and new services needs Windows and the .NET 10 SDK, and the steps are in the repo.</footer>
`;

writeFileSync(join(out, "index.html"), page({
  repo: "wcf-to-modern-dotnet-migration",
  product: "WCF to modern .NET migration",
  title: "WCF migration results",
  lead: "Test results, recorded SOAP traffic and code diffs from a WCF to ASP.NET Core migration.",
  body,
  styles: ["site.css"],
  scripts: ["site.js"],
}));
console.log(`built docs-site/ with ${golden.length} golden exchanges and ${total} tests reported`);
