import * as d3 from 'd3';

// Common D3 utilities for visualizations

/**
 * Create a zoom behavior with standard configuration
 */
export function createZoomBehavior(
  scaleExtent: [number, number] = [0.1, 10],
  onZoom?: (transform: d3.ZoomTransform) => void
) {
  const zoom = d3.zoom<SVGSVGElement, unknown>()
    .scaleExtent(scaleExtent)
    .on('zoom', (event) => {
      if (onZoom) {
        onZoom(event.transform);
      }
    });
  
  return zoom;
}

/**
 * Standard tooltip creation and positioning
 */
export function createTooltip(className: string = 'custom-tooltip') {
  return d3.select('body')
    .append('div')
    .attr('class', className)
    .style('position', 'absolute')
    .style('visibility', 'hidden')
    .style('background-color', 'rgba(0, 0, 0, 0.8)')
    .style('color', 'white')
    .style('padding', '8px 12px')
    .style('border-radius', '4px')
    .style('font-size', '12px')
    .style('pointer-events', 'none')
    .style('z-index', '1000')
    .style('max-width', '300px');
}

/**
 * Show tooltip at mouse position
 */
export function showTooltip(
  tooltip: d3.Selection<HTMLDivElement, unknown, HTMLElement, any>,
  content: string,
  event: MouseEvent
) {
  tooltip
    .html(content)
    .style('visibility', 'visible')
    .style('left', `${event.pageX + 10}px`)
    .style('top', `${event.pageY - 10}px`);
}

/**
 * Hide tooltip
 */
export function hideTooltip(
  tooltip: d3.Selection<HTMLDivElement, unknown, HTMLElement, any>
) {
  tooltip.style('visibility', 'hidden');
}

/**
 * Add drag behavior to nodes
 */
export function createDragBehavior<T extends d3.SimulationNodeDatum>(
  simulation: d3.Simulation<T, undefined>
) {
  function dragstarted(event: d3.D3DragEvent<any, T, T>) {
    if (!event.active) simulation.alphaTarget(0.3).restart();
    event.subject.fx = event.subject.x;
    event.subject.fy = event.subject.y;
  }
  
  function dragged(event: d3.D3DragEvent<any, T, T>) {
    event.subject.fx = event.x;
    event.subject.fy = event.y;
  }
  
  function dragended(event: d3.D3DragEvent<any, T, T>) {
    if (!event.active) simulation.alphaTarget(0);
    event.subject.fx = null;
    event.subject.fy = null;
  }
  
  return d3.drag<any, T>()
    .on('start', dragstarted)
    .on('drag', dragged)
    .on('end', dragended);
}

/**
 * Format numbers with appropriate suffixes
 */
export function formatNumber(num: number): string {
  if (num >= 1000000) {
    return (num / 1000000).toFixed(1) + 'M';
  } else if (num >= 1000) {
    return (num / 1000).toFixed(1) + 'K';
  }
  return num.toString();
}
