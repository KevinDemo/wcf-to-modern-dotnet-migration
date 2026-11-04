const $ = (id) => document.getElementById(id);
const golden = await fetch("golden.json").then((r) => r.json());

function show(index) {
  const item = golden[index];
  $("golden-status").textContent = item.status;
  $("golden-request").textContent = item.request;
  $("golden-response").textContent = item.response;
}

$("golden-pick").replaceChildren(...golden.map((g, i) => {
  const option = document.createElement("option");
  option.value = i;
  option.textContent = g.label;
  return option;
}));
$("golden-pick").addEventListener("change", (e) => show(Number(e.target.value)));
show(0);
